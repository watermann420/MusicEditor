#pragma once

#include <string>
#include <windows.h>
#include <functional>
#include <atomic>
#include <thread>

class MusicEngineHost
{
public:
    void Initialize();
    void Shutdown();
    void SetOutputSink(std::function<void(const std::wstring&)> sink);
    void SetScript(std::wstring script);
    void SaveScript();
    bool Start();
    bool Start(bool sleepOnStart, bool writeScript);
    bool Refresh();
    bool Sleep();
    bool Wake();
    bool OpenPlugin(const std::wstring& name);
    void Stop();
    bool IsRunning() const;

private:
    bool ResolvePaths();
    bool SendCommand(const char* command);
    bool _initialized = false;
    bool _running = false;
    std::wstring _script;
    std::wstring _engineExePath;
    std::wstring _scriptPath;
    HANDLE _processHandle = nullptr;
    HANDLE _threadHandle = nullptr;
    HANDLE _stdoutRead = nullptr;
    HANDLE _stdinWrite = nullptr;
    std::function<void(const std::wstring&)> _outputSink;
    std::atomic<bool> _reading{ false };
    std::thread _outputThread;
};
