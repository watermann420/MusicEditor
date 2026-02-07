#include "app/MusicEngineHost.h"

#include <filesystem>
#include <fstream>
#include <string>
#include <cstring>
#include <utility>
#include <vector>
#include <algorithm>

namespace
{
    constexpr char kRefreshCommand[] = "/S\n";
    constexpr char kPlayCommand[] = "/PLAY\n";
    constexpr char kStopCommand[] = "/STOP\n";
    constexpr char kExitCommand[] = "/EXIT\n";

    std::wstring FindMusicEngineRoot()
    {
        std::filesystem::path cwd = std::filesystem::current_path();
        std::filesystem::path candidates[] = {
            cwd,
            cwd / "MusicEngine",
            cwd.parent_path() / "MusicEngine",
            cwd.parent_path().parent_path() / "MusicEngine"
        };

        for (const auto& path : candidates)
        {
            if (std::filesystem::exists(path / "MusicEngine.csproj"))
            {
                return path.wstring();
            }
        }

        return {};
    }

    std::wstring FindEngineExe(const std::filesystem::path& root)
    {
        std::filesystem::path binPath = root / "bin";
        if (!std::filesystem::exists(binPath))
        {
            return {};
        }

        std::filesystem::path bestPath;
        std::filesystem::file_time_type bestTime{};

        std::filesystem::path configs[] = {
            binPath / "Debug",
            binPath / "Release"
        };

        for (const auto& configPath : configs)
        {
            if (!std::filesystem::exists(configPath))
            {
                continue;
            }

            for (const auto& entry : std::filesystem::recursive_directory_iterator(configPath))
            {
                if (!entry.is_regular_file())
                {
                    continue;
                }
                if (entry.path().filename() == "MusicEngine.exe")
                {
                    auto writeTime = entry.last_write_time();
                    if (bestPath.empty() || writeTime > bestTime)
                    {
                        bestPath = entry.path();
                        bestTime = writeTime;
                    }
                }
            }
        }

        for (const auto& entry : std::filesystem::recursive_directory_iterator(binPath))
        {
            if (!entry.is_regular_file())
            {
                continue;
            }
            if (entry.path().filename() == "MusicEngine.exe")
            {
                auto writeTime = entry.last_write_time();
                if (bestPath.empty() || writeTime > bestTime)
                {
                    bestPath = entry.path();
                    bestTime = writeTime;
                }
            }
        }

        if (bestPath.empty())
        {
            return {};
        }

        return bestPath.wstring();
    }

    std::string WideToUtf8(const std::wstring& text)
    {
        if (text.empty())
        {
            return {};
        }

        int size = WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
        if (size <= 0)
        {
            return {};
        }

        std::string result(static_cast<size_t>(size), '\0');
        WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), result.data(), size, nullptr, nullptr);
        return result;
    }

    std::wstring Utf8ToWide(const char* data, int length)
    {
        if (!data || length <= 0)
        {
            return {};
        }

        int size = MultiByteToWideChar(CP_UTF8, 0, data, length, nullptr, 0);
        if (size <= 0)
        {
            size = MultiByteToWideChar(CP_ACP, 0, data, length, nullptr, 0);
            if (size <= 0)
            {
                return {};
            }
            std::wstring result(static_cast<size_t>(size), L'\0');
            MultiByteToWideChar(CP_ACP, 0, data, length, result.data(), size);
            return result;
        }

        std::wstring result(static_cast<size_t>(size), L'\0');
        MultiByteToWideChar(CP_UTF8, 0, data, length, result.data(), size);
        return result;
    }
}

void MusicEngineHost::Initialize()
{
    _initialized = ResolvePaths();
    _running = false;
}

void MusicEngineHost::Shutdown()
{
    Stop();
    _initialized = false;
}

void MusicEngineHost::SetOutputSink(std::function<void(const std::wstring&)> sink)
{
    _outputSink = std::move(sink);
}

void MusicEngineHost::SetScript(std::wstring script)
{
    _script = std::move(script);
}

bool MusicEngineHost::Start()
{
    return Start(false, true);
}

bool MusicEngineHost::Start(bool sleepOnStart, bool writeScript)
{
    if (_running)
    {
        return true;
    }

    if (!_initialized)
    {
        _initialized = ResolvePaths();
    }

    if (_engineExePath.empty() || _scriptPath.empty())
    {
        return false;
    }

    if (writeScript)
    {
        SaveScript();
    }

    SECURITY_ATTRIBUTES sa{};
    sa.nLength = sizeof(sa);
    sa.bInheritHandle = TRUE;

    HANDLE stdoutRead = nullptr;
    HANDLE stdoutWrite = nullptr;
    if (!CreatePipe(&stdoutRead, &stdoutWrite, &sa, 0))
    {
        return false;
    }
    SetHandleInformation(stdoutRead, HANDLE_FLAG_INHERIT, 0);

    HANDLE stdinRead = nullptr;
    HANDLE stdinWrite = nullptr;
    if (!CreatePipe(&stdinRead, &stdinWrite, &sa, 0))
    {
        CloseHandle(stdoutRead);
        CloseHandle(stdoutWrite);
        return false;
    }
    SetHandleInformation(stdinWrite, HANDLE_FLAG_INHERIT, 0);

    STARTUPINFOW si{};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESTDHANDLES;
    si.hStdOutput = stdoutWrite;
    si.hStdError = stdoutWrite;
    si.hStdInput = stdinRead;
    PROCESS_INFORMATION pi{};
    std::wstring workingDir = std::filesystem::path(_scriptPath).parent_path().wstring();
    std::wstring commandLine = L"\"" + _engineExePath + L"\" --editor";

    BOOL created = CreateProcessW(
        nullptr,
        commandLine.empty() ? nullptr : &commandLine[0],
        nullptr,
        nullptr,
        TRUE,
        CREATE_NO_WINDOW,
        nullptr,
        workingDir.empty() ? nullptr : workingDir.c_str(),
        &si,
        &pi);

    CloseHandle(stdoutWrite);
    CloseHandle(stdinRead);

    if (!created)
    {
        CloseHandle(stdoutRead);
        CloseHandle(stdinWrite);
        return false;
    }

    _processHandle = pi.hProcess;
    _threadHandle = pi.hThread;
    _stdoutRead = stdoutRead;
    _stdinWrite = stdinWrite;
    _running = true;

    if (_outputSink)
    {
        _reading.store(true);
        _outputThread = std::thread([this]()
            {
                std::vector<char> buffer(4096);
                while (_reading.load())
                {
                    DWORD bytesRead = 0;
                    BOOL ok = ReadFile(_stdoutRead, buffer.data(), static_cast<DWORD>(buffer.size()), &bytesRead, nullptr);
                    if (!ok || bytesRead == 0)
                    {
                        break;
                    }

                    std::wstring chunk = Utf8ToWide(buffer.data(), static_cast<int>(bytesRead));
                    if (!chunk.empty() && _outputSink)
                    {
                        _outputSink(chunk);
                    }
                }
            });
    }

    if (sleepOnStart)
    {
        SendCommand(kStopCommand);
    }

    return true;
}

void MusicEngineHost::SaveScript()
{
    if (_scriptPath.empty() || _script.empty())
    {
        return;
    }

    std::ofstream out(std::filesystem::path(_scriptPath), std::ios::binary);
    if (out)
    {
        std::string utf8 = WideToUtf8(_script);
        out.write(utf8.data(), static_cast<std::streamsize>(utf8.size()));
    }
}

bool MusicEngineHost::Refresh()
{
    SaveScript();

    return SendCommand(kRefreshCommand);
}

bool MusicEngineHost::Sleep()
{
    return SendCommand(kStopCommand);
}

bool MusicEngineHost::Wake()
{
    return SendCommand(kPlayCommand);
}

bool MusicEngineHost::OpenPlugin(const std::wstring& name)
{
    if (name.empty())
    {
        return false;
    }

    std::string utf8Name = WideToUtf8(name);
    if (utf8Name.empty())
    {
        return false;
    }

    utf8Name.erase(std::remove(utf8Name.begin(), utf8Name.end(), '\r'), utf8Name.end());
    utf8Name.erase(std::remove(utf8Name.begin(), utf8Name.end(), '\n'), utf8Name.end());

    std::string command = "/open ";
    command += utf8Name;
    command.push_back('\n');

    return SendCommand(command.c_str());
}

bool MusicEngineHost::SendCommand(const char* command)
{
    if (!_running || !_stdinWrite || command == nullptr)
    {
        return false;
    }

    DWORD written = 0;
    return WriteFile(_stdinWrite, command, static_cast<DWORD>(std::strlen(command)), &written, nullptr) != FALSE;
}

void MusicEngineHost::Stop()
{
    if (_stdinWrite)
    {
        SendCommand(kExitCommand);
    }

    if (_reading.exchange(false))
    {
        if (_stdoutRead)
        {
            CloseHandle(_stdoutRead);
            _stdoutRead = nullptr;
        }
        if (_outputThread.joinable())
        {
            _outputThread.join();
        }
    }
    else if (_stdoutRead)
    {
        CloseHandle(_stdoutRead);
        _stdoutRead = nullptr;
    }

    if (_stdinWrite)
    {
        CloseHandle(_stdinWrite);
        _stdinWrite = nullptr;
    }

    if (_processHandle)
    {
        WaitForSingleObject(_processHandle, 2000);
        TerminateProcess(_processHandle, 0);
        WaitForSingleObject(_processHandle, 2000);
        CloseHandle(_processHandle);
        _processHandle = nullptr;
    }

    if (_threadHandle)
    {
        CloseHandle(_threadHandle);
        _threadHandle = nullptr;
    }

    _running = false;
}

bool MusicEngineHost::IsRunning() const
{
    return _running;
}

bool MusicEngineHost::ResolvePaths()
{
    std::wstring root = FindMusicEngineRoot();
    if (root.empty())
    {
        _engineExePath.clear();
        _scriptPath.clear();
        return false;
    }

    std::filesystem::path rootPath(root);
    _scriptPath = (rootPath / "test_script.csx").wstring();
    _engineExePath = FindEngineExe(rootPath);
    return !_engineExePath.empty();
}
