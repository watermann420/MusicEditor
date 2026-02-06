#include "editor/EditorView.h"
#include "app/CommandIds.h"

#include <string>
#include <algorithm>

namespace
{
    constexpr COLORREF kBgColor = RGB(18, 18, 20);
    constexpr COLORREF kTextColor = RGB(225, 225, 225);
    constexpr COLORREF kGutterColor = RGB(24, 24, 28);
    constexpr COLORREF kGutterTextColor = RGB(140, 140, 140);
    constexpr int kEditorPadding = 16;
    constexpr int kGutterMinWidth = 36;
}

void EditorView::Initialize(HWND parent)
{
    _parent = parent;
    _bgBrush = CreateSolidBrush(kBgColor);
    _font = CreateFontW(
        18, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_MODERN, L"Consolas");

    _editor = CreateWindowExW(
        0,
        L"EDIT",
        L"// MusicEngine Editor\n// Write your MusicEngine script here and press Play.\n\n",
        WS_CHILD | WS_VISIBLE | WS_HSCROLL |
            ES_LEFT | ES_MULTILINE | ES_AUTOVSCROLL | ES_AUTOHSCROLL,
        0, 0, 0, 0,
        parent,
        nullptr,
        reinterpret_cast<HINSTANCE>(GetWindowLongPtr(parent, GWLP_HINSTANCE)),
        nullptr);

    if (_editor)
    {
        SendMessageW(_editor, WM_SETFONT, reinterpret_cast<WPARAM>(_font), TRUE);
        SetWindowLongPtrW(_editor, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(this));
        _originalEditProc = reinterpret_cast<WNDPROC>(SetWindowLongPtrW(_editor, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(EditProc)));
    }

    if (_font && _editor)
    {
        HDC hdc = GetDC(_editor);
        HFONT oldFont = static_cast<HFONT>(SelectObject(hdc, _font));
        TEXTMETRICW tm{};
        GetTextMetricsW(hdc, &tm);
        _lineHeight = tm.tmHeight + tm.tmExternalLeading;
        SelectObject(hdc, oldFont);
        ReleaseDC(_editor, hdc);
    }

    Resize(0, 0);
}

void EditorView::Resize(int topOffset, int bottomOffset)
{
    if (!_editor || !_parent)
    {
        return;
    }

    UpdateGutterWidth();

    RECT rect{};
    GetClientRect(_parent, &rect);
    const int width = rect.right - rect.left;
    const int height = rect.bottom - rect.top;
    const int x = kEditorPadding + _gutterWidth;
    const int y = kEditorPadding + topOffset;
    const int w = width - (kEditorPadding * 2) - _gutterWidth;
    const int h = height - (kEditorPadding * 2) - topOffset - bottomOffset;
    MoveWindow(_editor, x, y, w, h, TRUE);
}

void EditorView::Shutdown()
{
    if (_font)
    {
        DeleteObject(_font);
        _font = nullptr;
    }
    if (_bgBrush)
    {
        DeleteObject(_bgBrush);
        _bgBrush = nullptr;
    }
}

LRESULT EditorView::OnEditColor(HDC hdc)
{
    SetTextColor(hdc, kTextColor);
    SetBkColor(hdc, kBgColor);
    return reinterpret_cast<LRESULT>(_bgBrush);
}

void EditorView::DrawLineNumbers(HDC hdc)
{
    if (!_parent || !_editor || !hdc)
    {
        return;
    }

    RECT editRect{};
    GetWindowRect(_editor, &editRect);
    POINT topLeft{ editRect.left, editRect.top };
    POINT bottomRight{ editRect.right, editRect.bottom };
    ScreenToClient(_parent, &topLeft);
    ScreenToClient(_parent, &bottomRight);

    RECT gutter{
        topLeft.x - _gutterWidth,
        topLeft.y,
        topLeft.x,
        bottomRight.y
    };

    HBRUSH gutterBrush = CreateSolidBrush(kGutterColor);
    FillRect(hdc, &gutter, gutterBrush);
    DeleteObject(gutterBrush);

    int firstLine = static_cast<int>(SendMessageW(_editor, EM_GETFIRSTVISIBLELINE, 0, 0));
    int totalLines = GetLineCount();
    int visibleLines = _lineHeight > 0 ? (gutter.bottom - gutter.top) / _lineHeight + 1 : 0;

    HFONT oldFont = _font ? static_cast<HFONT>(SelectObject(hdc, _font)) : nullptr;
    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, kGutterTextColor);

    for (int i = 0; i < visibleLines; ++i)
    {
        int lineNo = firstLine + i + 1;
        if (lineNo > totalLines)
        {
            break;
        }

        int y = gutter.top + (i * _lineHeight);
        RECT lineRect{ gutter.left + 4, y, gutter.right - 6, y + _lineHeight };
        std::wstring text = std::to_wstring(lineNo);
        DrawTextW(hdc, text.c_str(), static_cast<int>(text.size()), &lineRect,
            DT_RIGHT | DT_VCENTER | DT_SINGLELINE);
    }

    if (oldFont)
    {
        SelectObject(hdc, oldFont);
    }

}

LRESULT CALLBACK EditorView::EditProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    EditorView* editor = reinterpret_cast<EditorView*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    HWND parent = GetParent(hwnd);

    if (editor)
    {
        switch (msg)
        {
        case WM_KEYDOWN:
            if (wParam == VK_RETURN && (GetKeyState(VK_CONTROL) & 0x8000))
            {
                PostMessageW(parent, WM_COMMAND, MAKEWPARAM(kCommandRefresh, 0), 0);
                return 0;
            }
            if (wParam == VK_ESCAPE)
            {
                PostMessageW(parent, WM_COMMAND, MAKEWPARAM(kCommandStop, 0), 0);
                return 0;
            }
            InvalidateRect(parent, nullptr, FALSE);
            break;
        case WM_VSCROLL:
        case WM_MOUSEWHEEL:
        case WM_KEYUP:
        case WM_CHAR:
        case WM_LBUTTONDOWN:
        case WM_LBUTTONUP:
        case WM_PASTE:
            InvalidateRect(parent, nullptr, FALSE);
            break;
        default:
            break;
        }
    }

    if (editor && editor->_originalEditProc)
    {
        return CallWindowProcW(editor->_originalEditProc, hwnd, msg, wParam, lParam);
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

void EditorView::UpdateGutterWidth()
{
    int lineCount = GetLineCount();
    int digits = 1;
    while (lineCount >= 10)
    {
        lineCount /= 10;
        ++digits;
    }

    if (_font && _editor)
    {
        HDC hdc = GetDC(_editor);
        HFONT oldFont = static_cast<HFONT>(SelectObject(hdc, _font));
        std::wstring sample(digits, L'0');
        SIZE size{};
        GetTextExtentPoint32W(hdc, sample.c_str(), static_cast<int>(sample.size()), &size);
        SelectObject(hdc, oldFont);
        ReleaseDC(_editor, hdc);
        _gutterWidth = std::max<int>(kGutterMinWidth, static_cast<int>(size.cx + 16));
    }
    else
    {
        _gutterWidth = std::max<int>(kGutterMinWidth, 10 * digits + 16);
    }
}

int EditorView::GetLineCount() const
{
    if (!_editor)
    {
        return 1;
    }
    return static_cast<int>(SendMessageW(_editor, EM_GETLINECOUNT, 0, 0));
}

std::wstring EditorView::GetText() const
{
    if (!_editor)
    {
        return {};
    }

    int length = GetWindowTextLengthW(_editor);
    if (length <= 0)
    {
        return {};
    }

    std::wstring text(static_cast<size_t>(length) + 1, L'\0');
    GetWindowTextW(_editor, text.data(), length + 1);
    text.resize(static_cast<size_t>(length));
    return text;
}

void EditorView::SetText(const std::wstring& text)
{
    if (!_editor)
    {
        return;
    }

    SetWindowTextW(_editor, text.c_str());
    InvalidateRect(_parent, nullptr, FALSE);
}
