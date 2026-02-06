#include "app/App.h"
#include "app/CommandIds.h"

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>
#include <windowsx.h>

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
    constexpr COLORREF kConsoleTextColor = RGB(190, 190, 190);
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
    case WM_DESTROY:
        if (app)
        {
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
        if (ShouldSuppressConsoleLine(line))
        {
            continue;
        }

        const int length = GetWindowTextLengthW(_console);
        SendMessageW(_console, EM_SETSEL, length, length);
        SendMessageW(_console, EM_REPLACESEL, FALSE, reinterpret_cast<LPARAM>(line.c_str()));
    }
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
    if (!trimmed.empty() && trimmed[0] == L'>')
    {
        return true;
    }

    return false;
}
