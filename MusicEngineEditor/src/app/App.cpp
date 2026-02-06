#include "app/App.h"

namespace
{
    constexpr int kWindowWidth = 1280;
    constexpr int kWindowHeight = 720;
    constexpr wchar_t kWindowClass[] = L"MusicEngineEditorWindow";
    constexpr wchar_t kWindowTitle[] = L"MusicEngine Editor";
    constexpr COLORREF kBgColor = RGB(18, 18, 20);
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
    wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);

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
    _editor.Initialize(_window);

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
            app->_editor.Resize();
        }
        return 0;
    case WM_CTLCOLOREDIT:
        if (app)
        {
            return app->_editor.OnEditColor(reinterpret_cast<HDC>(wParam));
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
    case WM_PAINT:
        if (app)
        {
            app->_editor.DrawLineNumbers();
            return 0;
        }
        break;
    case WM_DESTROY:
        if (app)
        {
            app->_editor.Shutdown();
            if (app->_backgroundBrush)
            {
                DeleteObject(app->_backgroundBrush);
                app->_backgroundBrush = nullptr;
            }
        }
        PostQuitMessage(0);
        return 0;
    default:
        break;
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
}
