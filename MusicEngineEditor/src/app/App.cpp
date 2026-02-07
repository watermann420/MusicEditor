#include "app/App.h"
#include "app/CommandIds.h"

#include <algorithm>
#include <cwctype>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>
#include <windowsx.h>
#include <dwmapi.h>
#include <uxtheme.h>

#pragma comment(lib, "dwmapi.lib")
#pragma comment(lib, "uxtheme.lib")

namespace
{
    constexpr int kWindowWidth = 1280;
    constexpr int kWindowHeight = 720;
    constexpr wchar_t kWindowClass[] = L"MusicEngineEditorWindow";
    constexpr wchar_t kWindowTitle[] = L"MusicEngine Editor";
    constexpr COLORREF kBgColor = RGB(18, 18, 20);
    constexpr COLORREF kTopBarColor = RGB(12, 12, 14);
    constexpr COLORREF kTopBarTextColor = RGB(235, 235, 235);
    constexpr COLORREF kPlayColor = RGB(30, 160, 70);
    constexpr COLORREF kStopColor = RGB(190, 60, 60);
    constexpr COLORREF kConsoleBgColor = RGB(12, 12, 14);
    constexpr COLORREF kConsoleTextColor = RGB(230, 230, 230);
    constexpr int kTopBarHeight = 52;
    constexpr int kTopBarPadding = 12;
    constexpr int kPlayButtonWidth = 110;
    constexpr int kPlayButtonHeight = 28;
    constexpr int kConsoleHeight = 160;
    constexpr int kConsoleMinHeight = 90;
    constexpr int kConsolePadding = 10;
    constexpr int kSplitterHeight = 6;
    constexpr int kConsoleToggleButtonWidth = 120;
    constexpr UINT kOutputMessage = WM_APP + 1;
    constexpr int kEditorPadding = 16;

    enum PreferredAppMode
    {
        Default,
        AllowDark,
        ForceDark,
        ForceLight,
        Max
    };

    using SetPreferredAppModeFn = PreferredAppMode(WINAPI*)(PreferredAppMode);
    using AllowDarkModeForWindowFn = BOOL(WINAPI*)(HWND, BOOL);
    using RefreshImmersiveColorPolicyStateFn = void (WINAPI*)();

    struct DarkModeApi
    {
        HMODULE module = nullptr;
        SetPreferredAppModeFn setPreferredAppMode = nullptr;
        AllowDarkModeForWindowFn allowDarkModeForWindow = nullptr;
        RefreshImmersiveColorPolicyStateFn refreshImmersiveColorPolicyState = nullptr;
        bool initialized = false;
    };

    DarkModeApi& GetDarkModeApi()
    {
        static DarkModeApi api;
        if (!api.initialized)
        {
            api.module = LoadLibraryW(L"uxtheme.dll");
            if (api.module)
            {
                api.setPreferredAppMode = reinterpret_cast<SetPreferredAppModeFn>(
                    GetProcAddress(api.module, "SetPreferredAppMode"));
                api.allowDarkModeForWindow = reinterpret_cast<AllowDarkModeForWindowFn>(
                    GetProcAddress(api.module, "AllowDarkModeForWindow"));
                api.refreshImmersiveColorPolicyState = reinterpret_cast<RefreshImmersiveColorPolicyStateFn>(
                    GetProcAddress(api.module, "RefreshImmersiveColorPolicyState"));
            }
            api.initialized = true;
        }
        return api;
    }

    void EnableDarkModeForWindow(HWND hwnd)
    {
        DarkModeApi& api = GetDarkModeApi();
        if (api.setPreferredAppMode)
        {
            api.setPreferredAppMode(AllowDark);
        }
        if (api.refreshImmersiveColorPolicyState)
        {
            api.refreshImmersiveColorPolicyState();
        }
        if (api.allowDarkModeForWindow)
        {
            api.allowDarkModeForWindow(hwnd, TRUE);
        }

        const BOOL enable = TRUE;
        if (FAILED(DwmSetWindowAttribute(hwnd, 20, &enable, sizeof(enable))))
        {
            DwmSetWindowAttribute(hwnd, 19, &enable, sizeof(enable));
        }
    }

    void ApplyDarkThemeToControl(HWND hwnd)
    {
        DarkModeApi& api = GetDarkModeApi();
        if (api.allowDarkModeForWindow)
        {
            api.allowDarkModeForWindow(hwnd, TRUE);
        }
        SetWindowTheme(hwnd, L"DarkMode_Explorer", nullptr);
    }

    std::wstring ReadFileUtf8(const std::filesystem::path& path)
    {
        std::ifstream file(path, std::ios::binary);
        if (!file)
        {
            return {};
        }

        std::vector<char> bytes((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
        if (bytes.empty())
        {
            return {};
        }

        int wideSize = MultiByteToWideChar(CP_UTF8, 0, bytes.data(), static_cast<int>(bytes.size()), nullptr, 0);
        if (wideSize <= 0)
        {
            return {};
        }

        std::wstring wide(static_cast<size_t>(wideSize), L'\0');
        MultiByteToWideChar(CP_UTF8, 0, bytes.data(), static_cast<int>(bytes.size()), wide.data(), wideSize);
        return wide;
    }

    std::wstring LoadStartupScript()
    {
        std::filesystem::path cwd = std::filesystem::current_path();
        std::filesystem::path candidates[] = {
            cwd / "test_script.csx",
            cwd / "MusicEngine" / "test_script.csx",
            cwd.parent_path() / "MusicEngine" / "test_script.csx",
            cwd.parent_path().parent_path() / "MusicEngine" / "test_script.csx"
        };

        for (const auto& path : candidates)
        {
            if (std::filesystem::exists(path))
            {
                std::wstring contents = ReadFileUtf8(path);
                if (!contents.empty())
                {
                    return contents;
                }
            }
        }

        return {};
    }

    bool TryParseNoteEvent(const std::wstring& line, int& note, bool& isOn)
    {
        if (line.empty())
        {
            return false;
        }

        std::wstring upper;
        upper.reserve(line.size());
        for (wchar_t ch : line)
        {
            upper.push_back(static_cast<wchar_t>(towupper(ch)));
        }

        bool on = false;
        bool off = false;
        size_t tokenPos = std::wstring::npos;
        size_t tokenLen = 0;
        if ((tokenPos = upper.find(L"NOTE_ON")) != std::wstring::npos)
        {
            on = true;
            tokenLen = 7;
        }
        else if ((tokenPos = upper.find(L"NOTE ON")) != std::wstring::npos)
        {
            on = true;
            tokenLen = 7;
        }
        else if ((tokenPos = upper.find(L"NOTEON")) != std::wstring::npos)
        {
            on = true;
            tokenLen = 6;
        }
        else if ((tokenPos = upper.find(L"NOTE_OFF")) != std::wstring::npos)
        {
            off = true;
            tokenLen = 8;
        }
        else if ((tokenPos = upper.find(L"NOTE OFF")) != std::wstring::npos)
        {
            off = true;
            tokenLen = 8;
        }
        else if ((tokenPos = upper.find(L"NOTEOFF")) != std::wstring::npos)
        {
            off = true;
            tokenLen = 7;
        }

        if (!on && !off)
        {
            return false;
        }

        int value = -1;
        int velocity = -1;
        if (tokenPos != std::wstring::npos)
        {
            size_t i = tokenPos + tokenLen;
            auto parseNextInt = [&](size_t& index, int& outValue) -> bool
            {
                while (index < line.size() && !iswdigit(line[index]))
                {
                    ++index;
                }
                if (index >= line.size())
                {
                    return false;
                }
                int current = 0;
                while (index < line.size() && iswdigit(line[index]))
                {
                    current = current * 10 + (line[index] - L'0');
                    ++index;
                }
                outValue = current;
                return true;
            };

            parseNextInt(i, value);
            parseNextInt(i, velocity);
        }

        if (value < 0)
        {
            return false;
        }

        note = value;
        isOn = on && !off;
        if (isOn && velocity == 0)
        {
            isOn = false;
        }
        return true;
    }

    bool IsIdentifierStart(wchar_t ch)
    {
        return (ch >= L'a' && ch <= L'z') || (ch >= L'A' && ch <= L'Z') || ch == L'_';
    }

    bool IsIdentifierChar(wchar_t ch)
    {
        return IsIdentifierStart(ch) || (ch >= L'0' && ch <= L'9');
    }

    bool TryExtractVstBinding(const std::wstring& line, std::wstring& outVar, std::wstring& outName)
    {
        size_t commentPos = line.find(L"//");
        std::wstring view = commentPos == std::wstring::npos ? line : line.substr(0, commentPos);

        const std::wstring token = L"CreateVst";
        size_t createPos = view.find(token);
        if (createPos == std::wstring::npos)
        {
            return false;
        }
        size_t tokenEnd = createPos + token.size();
        if ((createPos > 0 && IsIdentifierChar(view[createPos - 1])) ||
            (tokenEnd < view.size() && IsIdentifierChar(view[tokenEnd])))
        {
            return false;
        }

        size_t eqPos = view.rfind(L'=', createPos);
        if (eqPos == std::wstring::npos)
        {
            return false;
        }

        size_t idEnd = eqPos;
        while (idEnd > 0 && iswspace(view[idEnd - 1]))
        {
            --idEnd;
        }
        size_t idStart = idEnd;
        while (idStart > 0 && IsIdentifierChar(view[idStart - 1]))
        {
            --idStart;
        }
        if (idStart == idEnd)
        {
            return false;
        }

        size_t quoteStart = view.find(L'"', createPos);
        if (quoteStart == std::wstring::npos)
        {
            return false;
        }
        size_t quoteEnd = view.find(L'"', quoteStart + 1);
        if (quoteEnd == std::wstring::npos || quoteEnd <= quoteStart + 1)
        {
            return false;
        }

        outVar = view.substr(idStart, idEnd - idStart);
        outName = view.substr(quoteStart + 1, quoteEnd - quoteStart - 1);
        return !outVar.empty() && !outName.empty();
    }

    bool TryFindVstPluginName(const std::wstring& script, const std::wstring& identifier, std::wstring& outName)
    {
        if (identifier.empty())
        {
            return false;
        }

        size_t start = 0;
        while (start < script.size())
        {
            size_t end = script.find(L'\n', start);
            if (end == std::wstring::npos)
            {
                end = script.size();
            }

            std::wstring line = script.substr(start, end - start);
            if (!line.empty() && line.back() == L'\r')
            {
                line.pop_back();
            }

            std::wstring varName;
            std::wstring pluginName;
            if (TryExtractVstBinding(line, varName, pluginName) && varName == identifier)
            {
                outName = std::move(pluginName);
                return true;
            }

            start = end + 1;
        }

        return false;
    }
}

int App::Run(HINSTANCE instance, int nCmdShow)
{
    if (!InitWindow(instance, nCmdShow))
    {
        return 1;
    }

    MSG msg{};
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    return 0;
}

bool App::InitWindow(HINSTANCE instance, int nCmdShow)
{
    WNDCLASSW wc{};
    wc.lpfnWndProc = App::WndProc;
    wc.hInstance = instance;
    wc.lpszClassName = kWindowClass;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = nullptr;

    if (!RegisterClassW(&wc))
    {
        return false;
    }

    _window = CreateWindowExW(
        0,
        kWindowClass,
        kWindowTitle,
        WS_OVERLAPPEDWINDOW | WS_VISIBLE,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        kWindowWidth,
        kWindowHeight,
        nullptr,
        nullptr,
        instance,
        this);

    if (!_window)
    {
        return false;
    }

    EnableDarkModeForWindow(_window);

    _backgroundBrush = CreateSolidBrush(kBgColor);
    _topBarBrush = CreateSolidBrush(kTopBarColor);
    _consoleBrush = CreateSolidBrush(kConsoleBgColor);
    _uiFont = CreateFontW(
        16, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Segoe UI");

    _playButton = CreateWindowExW(
        0,
        L"BUTTON",
        L"Play",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        0, 0, 0, 0,
        _window,
        reinterpret_cast<HMENU>(static_cast<intptr_t>(kCommandPlayToggle)),
        instance,
        nullptr);

    _consoleToggleButton = CreateWindowExW(
        0,
        L"BUTTON",
        L"Console",
        WS_CHILD | WS_VISIBLE,
        0, 0, 0, 0,
        _window,
        reinterpret_cast<HMENU>(static_cast<intptr_t>(kCommandToggleConsole)),
        instance,
        nullptr);

    _console = CreateWindowExW(
        0,
        L"EDIT",
        L"",
        WS_CHILD | WS_VISIBLE | WS_VSCROLL | ES_LEFT | ES_MULTILINE | ES_AUTOVSCROLL | ES_READONLY,
        0, 0, 0, 0,
        _window,
        nullptr,
        instance,
        nullptr);

    if (_console)
    {
        ApplyDarkThemeToControl(_console);
    }

    if (_playButton && _uiFont)
    {
        SendMessageW(_playButton, WM_SETFONT, reinterpret_cast<WPARAM>(_uiFont), TRUE);
    }
    if (_consoleToggleButton && _uiFont)
    {
        SendMessageW(_consoleToggleButton, WM_SETFONT, reinterpret_cast<WPARAM>(_uiFont), TRUE);
    }
    if (_console && _uiFont)
    {
        SendMessageW(_console, WM_SETFONT, reinterpret_cast<WPARAM>(_uiFont), TRUE);
    }

    _engine.Initialize();
    _engine.SetOutputSink([window = _window](const std::wstring& text)
        {
            auto* copy = new std::wstring(text);
            PostMessageW(window, kOutputMessage, 0, reinterpret_cast<LPARAM>(copy));
        });
    _editor.Initialize(_window);

    std::wstring startupScript = LoadStartupScript();
    if (!startupScript.empty())
    {
        _editor.SetText(startupScript);
    }

    LayoutControls();

    _engine.SetScript(_editor.GetText());
    _engine.Start(true, false);
    _isPlaying = false;
    UpdatePlayButton();

    SetTimer(_window, 1, 250, nullptr);

    ShowWindow(_window, nCmdShow);
    return true;
}

LRESULT CALLBACK App::WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    App* app = nullptr;
    if (msg == WM_NCCREATE)
    {
        auto create = reinterpret_cast<CREATESTRUCTW*>(lParam);
        app = reinterpret_cast<App*>(create->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(app));
    }
    else
    {
        app = reinterpret_cast<App*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    }

    switch (msg)
    {
    case WM_SIZE:
        if (app)
        {
            app->LayoutControls();
        }
        return 0;
    case WM_CTLCOLOREDIT:
    case WM_CTLCOLORSTATIC:
        if (app)
        {
            HWND control = reinterpret_cast<HWND>(lParam);
            if (control == app->_console)
            {
                HDC hdc = reinterpret_cast<HDC>(wParam);
                SetTextColor(hdc, kConsoleTextColor);
                SetBkColor(hdc, kConsoleBgColor);
                return reinterpret_cast<LRESULT>(app->_consoleBrush);
            }
            return app->_editor.OnEditColor(reinterpret_cast<HDC>(wParam));
        }
        break;
    case WM_COMMAND:
        if (app)
        {
            switch (LOWORD(wParam))
            {
            case kCommandPlayToggle:
                if (HIWORD(wParam) == BN_CLICKED)
                {
                    app->TogglePlayback();
                }
                return 0;
            case kCommandRefresh:
                app->RefreshPlayback();
                return 0;
            case kCommandStop:
                app->StopPlayback();
                return 0;
            case kCommandToggleConsole:
                if (HIWORD(wParam) == BN_CLICKED)
                {
                    app->ToggleConsole();
                }
                return 0;
            default:
                break;
            }
        }
        break;
    case WM_DRAWITEM:
        if (app)
        {
            auto drawItem = reinterpret_cast<DRAWITEMSTRUCT*>(lParam);
            if (drawItem && drawItem->CtlID == kCommandPlayToggle)
            {
                const COLORREF fill = app->_isPlaying ? kStopColor : kPlayColor;
                HBRUSH brush = CreateSolidBrush(fill);
                FillRect(drawItem->hDC, &drawItem->rcItem, brush);
                DeleteObject(brush);

                HFONT oldFont = nullptr;
                if (app->_uiFont)
                {
                    oldFont = static_cast<HFONT>(SelectObject(drawItem->hDC, app->_uiFont));
                }

                SetBkMode(drawItem->hDC, TRANSPARENT);
                SetTextColor(drawItem->hDC, kTopBarTextColor);
                const wchar_t* label = app->_isPlaying ? L"Stop" : L"Play";
                DrawTextW(drawItem->hDC, label, -1, &drawItem->rcItem,
                    DT_CENTER | DT_VCENTER | DT_SINGLELINE);

                FrameRect(drawItem->hDC, &drawItem->rcItem, reinterpret_cast<HBRUSH>(GetStockObject(BLACK_BRUSH)));

                if (oldFont)
                {
                    SelectObject(drawItem->hDC, oldFont);
                }
                return TRUE;
            }
        }
        break;
    case WM_ERASEBKGND:
        if (app && app->_backgroundBrush)
        {
            HDC hdc = reinterpret_cast<HDC>(wParam);
            RECT rect{};
            GetClientRect(hwnd, &rect);
            FillRect(hdc, &rect, app->_backgroundBrush);
            return 1;
        }
        break;
    case WM_LBUTTONDOWN:
        if (app)
        {
            POINT pt{ GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
            if (app->_editor.HandleGutterClick(pt))
            {
                return 0;
            }
            if (app->IsOverSplitter(pt))
            {
                app->_draggingSplitter = true;
                SetCapture(hwnd);
                return 0;
            }
        }
        break;
    case WM_LBUTTONUP:
        if (app && app->_draggingSplitter)
        {
            app->_draggingSplitter = false;
            ReleaseCapture();
            return 0;
        }
        break;
    case WM_MOUSEMOVE:
        if (app && app->_draggingSplitter)
        {
            RECT client{};
            GetClientRect(hwnd, &client);
            const int height = client.bottom - client.top;
            const int mouseY = GET_Y_LPARAM(lParam);
            int newHeight = height - mouseY - kConsolePadding;
            newHeight = std::clamp(newHeight, kConsoleMinHeight, height - kTopBarHeight - (kEditorPadding * 2));
            if (newHeight != app->_consoleHeight)
            {
                app->_consoleHeight = newHeight;
                app->LayoutControls();
                InvalidateRect(hwnd, nullptr, FALSE);
            }
            return 0;
        }
        break;
    case WM_SETCURSOR:
        if (app)
        {
            POINT pt{};
            GetCursorPos(&pt);
            ScreenToClient(hwnd, &pt);
            if (app->IsOverSplitter(pt))
            {
                SetCursor(LoadCursor(nullptr, IDC_SIZENS));
                return TRUE;
            }
        }
        break;
    case WM_PAINT:
        if (app)
        {
            PAINTSTRUCT ps{};
            HDC hdc = BeginPaint(hwnd, &ps);
            RECT client{};
            GetClientRect(hwnd, &client);

            if (app->_backgroundBrush)
            {
                FillRect(hdc, &client, app->_backgroundBrush);
            }

            RECT topBar{ 0, 0, client.right, kTopBarHeight };
            if (app->_topBarBrush)
            {
                FillRect(hdc, &topBar, app->_topBarBrush);
            }

            if (app->_consoleVisible)
            {
                RECT clientRect{};
                GetClientRect(hwnd, &clientRect);
                const int splitterTop = clientRect.bottom - kConsolePadding - app->_consoleHeight - kSplitterHeight;
                RECT splitter{ 0, splitterTop, clientRect.right, splitterTop + kSplitterHeight };
                FillRect(hdc, &splitter, app->_topBarBrush ? app->_topBarBrush : reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1));
            }

            app->_editor.DrawLineNumbers(hdc);
            EndPaint(hwnd, &ps);
            return 0;
        }
        break;
    case WM_TIMER:
        if (app && wParam == 1)
        {
            app->TickEditorVisuals();
            return 0;
        }
        break;
    case kOutputMessage:
        if (app)
        {
            auto* text = reinterpret_cast<std::wstring*>(lParam);
            if (text)
            {
                app->AppendConsoleText(*text);
                delete text;
            }
            return 0;
        }
        break;
    case kEditorOpenPluginMessage:
        if (app)
        {
            auto* word = reinterpret_cast<std::wstring*>(lParam);
            if (word)
            {
                app->OpenPluginForIdentifier(*word);
                delete word;
            }
            return 0;
        }
        break;
    case WM_DESTROY:
        if (app)
        {
            KillTimer(hwnd, 1);
            app->_editor.Shutdown();
            app->_engine.Shutdown();
            if (app->_uiFont)
            {
                DeleteObject(app->_uiFont);
                app->_uiFont = nullptr;
            }
            if (app->_backgroundBrush)
            {
                DeleteObject(app->_backgroundBrush);
                app->_backgroundBrush = nullptr;
            }
            if (app->_topBarBrush)
            {
                DeleteObject(app->_topBarBrush);
                app->_topBarBrush = nullptr;
            }
            if (app->_consoleBrush)
            {
                DeleteObject(app->_consoleBrush);
                app->_consoleBrush = nullptr;
            }
        }
        PostQuitMessage(0);
        return 0;
    default:
        break;
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

void App::LayoutControls()
{
    if (!_window)
    {
        return;
    }

    RECT client{};
    GetClientRect(_window, &client);
    const int width = client.right - client.left;
    const int height = client.bottom - client.top;
    const int buttonX = kTopBarPadding;
    const int buttonY = (kTopBarHeight - kPlayButtonHeight) / 2;

    if (_playButton)
    {
        MoveWindow(_playButton, buttonX, buttonY, kPlayButtonWidth, kPlayButtonHeight, TRUE);
    }
    if (_consoleToggleButton)
    {
        MoveWindow(_consoleToggleButton, buttonX + kPlayButtonWidth + 10, buttonY, kConsoleToggleButtonWidth,
            kPlayButtonHeight, TRUE);
    }

    int consoleHeight = _consoleHeight;
    if (!_consoleVisible)
    {
        consoleHeight = 0;
    }
    else if (height < (kTopBarHeight + kConsoleHeight + (kEditorPadding * 3)))
    {
        consoleHeight = std::max(kConsoleMinHeight, height / 4);
    }

    const int consoleX = kEditorPadding;
    const int consoleY = height - kConsolePadding - consoleHeight;
    const int consoleW = width - (kEditorPadding * 2);
    const int consoleH = consoleHeight;

    if (_console)
    {
        if (_consoleVisible)
        {
            ShowWindow(_console, SW_SHOW);
            MoveWindow(_console, consoleX, consoleY, consoleW, consoleH, TRUE);
        }
        else
        {
            ShowWindow(_console, SW_HIDE);
        }
    }

    int bottomOffset = consoleHeight + (_consoleVisible ? (kConsolePadding + kSplitterHeight) : kEditorPadding);
    _editor.Resize(kTopBarHeight, bottomOffset);
}

void App::TogglePlayback()
{
    if (_isPlaying)
    {
        StopPlayback();
    }
    else
    {
        StartPlayback();
    }
}

void App::StartPlayback()
{
    _engine.SetScript(_editor.GetText());
    bool started = false;
    if (_engine.IsRunning())
    {
        started = _engine.Refresh();
        if (started)
        {
            _engine.Wake();
        }
        if (!started)
        {
            _engine.Stop();
        }
    }
    else
    {
        started = _engine.Start(true, true);
        if (started)
        {
            started = _engine.Refresh();
            if (started)
            {
                _engine.Wake();
            }
        }
    }
    if (!started)
    {
        _engine.Stop();
        started = _engine.Start(true, true);
        if (started)
        {
            started = _engine.Refresh();
            if (started)
            {
                _engine.Wake();
            }
        }
    }

    _isPlaying = started;
    UpdatePlayButton();
}

void App::StopPlayback()
{
    if (!_engine.Sleep())
    {
        _engine.Stop();
    }
    _isPlaying = false;
    UpdatePlayButton();
}

void App::RefreshPlayback()
{
    _engine.SetScript(_editor.GetText());
    bool started = false;
    if (_engine.IsRunning())
    {
        started = _engine.Refresh();
        if (!started)
        {
            _engine.Stop();
        }
        if (started && _isPlaying)
        {
            _engine.Wake();
        }
    }
    else
    {
        started = _engine.Start(true, true);
        if (started)
        {
            started = _engine.Refresh();
            if (started && _isPlaying)
            {
                _engine.Wake();
            }
        }
    }
    if (!started)
    {
        _engine.Stop();
        started = _engine.Start(true, true);
        if (started)
        {
            started = _engine.Refresh();
            if (started && _isPlaying)
            {
                _engine.Wake();
            }
        }
    }

    _isPlaying = started;
    UpdatePlayButton();
}

void App::UpdatePlayButton()
{
    if (_playButton)
    {
        SetWindowTextW(_playButton, _isPlaying ? L"Stop" : L"Play");
        InvalidateRect(_playButton, nullptr, TRUE);
    }
}

void App::ToggleConsole()
{
    _consoleVisible = !_consoleVisible;
    if (_consoleToggleButton)
    {
        SetWindowTextW(_consoleToggleButton, _consoleVisible ? L"Console" : L"Console Hidden");
    }
    LayoutControls();
    InvalidateRect(_window, nullptr, TRUE);
}

bool App::IsOverSplitter(POINT pt) const
{
    if (!_consoleVisible)
    {
        return false;
    }

    RECT client{};
    GetClientRect(_window, &client);
    const int splitterTop = client.bottom - kConsolePadding - _consoleHeight - kSplitterHeight;
    RECT splitter{ 0, splitterTop, client.right, splitterTop + kSplitterHeight };
    return PtInRect(&splitter, pt) == TRUE;
}

void App::AppendConsoleText(const std::wstring& text)
{
    if (!_console || text.empty())
    {
        return;
    }

    _consolePending.append(text);

    size_t newline = 0;
    while ((newline = _consolePending.find(L'\n')) != std::wstring::npos)
    {
        std::wstring line = _consolePending.substr(0, newline + 1);
        _consolePending.erase(0, newline + 1);
        int noteValue = -1;
        bool noteOn = false;
        if (TryParseNoteEvent(line, noteValue, noteOn))
        {
            _editor.SetActiveNote(noteValue, noteOn);
        }

        if (ShouldSuppressConsoleLine(line))
        {
            continue;
        }

        const int length = GetWindowTextLengthW(_console);
        SendMessageW(_console, EM_SETSEL, length, length);
        SendMessageW(_console, EM_REPLACESEL, FALSE, reinterpret_cast<LPARAM>(line.c_str()));
    }
}

void App::TickEditorVisuals()
{
    _editor.PruneExpiredNotes();
}

void App::OpenPluginForIdentifier(const std::wstring& identifier)
{
    if (identifier.empty())
    {
        return;
    }

    std::wstring script = _editor.GetText();
    std::wstring pluginName;
    if (!TryFindVstPluginName(script, identifier, pluginName))
    {
        pluginName = identifier;
    }

    if (!_engine.IsRunning())
    {
        _engine.SetScript(std::move(script));
        _engine.Start(true, false);
    }

    _engine.OpenPlugin(pluginName);
}

bool App::ShouldSuppressConsoleLine(const std::wstring& line) const
{
    size_t start = 0;
    while (start < line.size() && iswspace(line[start]))
    {
        ++start;
    }

    size_t end = line.size();
    while (end > start && iswspace(line[end - 1]))
    {
        --end;
    }

    std::wstring trimmed = line.substr(start, end - start);
    if (trimmed == L"Commands: /S to Refresh, /exit to Stop, /vst to list, /open <name>.")
    {
        return true;
    }
    if (trimmed == L"Commands: /S to Refresh, /sleep, /wake, /exit to Stop, /vst to list, /open <name>.")
    {
        return true;
    }
    if (trimmed.find(L"Unknown command: /SLEEP") != std::wstring::npos)
    {
        return true;
    }
    if (trimmed.find(L"Unknown command: /WAKE") != std::wstring::npos)
    {
        return true;
    }
    if (trimmed.find(L"NOTE_ON") != std::wstring::npos || trimmed.find(L"NOTE OFF") != std::wstring::npos ||
        trimmed.find(L"NOTE_OFF") != std::wstring::npos || trimmed.find(L"NOTE ON") != std::wstring::npos ||
        trimmed.find(L"MIDI_IN") != std::wstring::npos || trimmed.find(L"MIDI_DEVICE_ACTIVE") != std::wstring::npos)
    {
        return true;
    }
    if (!trimmed.empty() && trimmed[0] == L'>')
    {
        return true;
    }

    return false;
}
