#include "editor/EditorView.h"
#include "app/CommandIds.h"

#include <algorithm>
#include <cstring>
#include <cwchar>
#include <cwctype>
#include <filesystem>
#include <fstream>
#include <string>
#include <windowsx.h>
#include <uxtheme.h>

#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "msimg32.lib")
#pragma comment(lib, "uxtheme.lib")

namespace
{
    constexpr COLORREF kBgColor = RGB(18, 18, 20);
    constexpr COLORREF kTextColor = RGB(225, 225, 225);
    constexpr COLORREF kGutterColor = RGB(24, 24, 28);
    constexpr COLORREF kGutterTextColor = RGB(140, 140, 140);
    constexpr int kEditorPadding = 16;
    constexpr int kGutterMinWidth = 36;
    constexpr int kGutterGap = 8;
    constexpr int kGutterTextPadding = 4;
    constexpr int kFoldIconAreaWidth = 14;
    constexpr int kFoldIconSize = 8;
    const wchar_t kFoldMarkerPrefix[] = L"/*fold:";

    const Gdiplus::Color kDefaultText(255, 230, 230, 230);
    const Gdiplus::Color kKeywordBlue(255, 120, 170, 255);
    const Gdiplus::Color kMethodGreen(255, 120, 210, 120);
    const Gdiplus::Color kPatternPink(255, 200, 90, 160);
    const Gdiplus::Color kPatternWhite(255, 240, 240, 240);
    const Gdiplus::Color kNumberPink(255, 255, 130, 190);
    const Gdiplus::Color kNameTurquoise(255, 80, 200, 200);
    const Gdiplus::Color kCommentGray(255, 150, 150, 150);
    const Gdiplus::Color kBoolTrueGreen(255, 110, 210, 110);
    const Gdiplus::Color kBoolFalseRed(255, 220, 90, 90);
    const Gdiplus::Color kVstNameOrange(255, 255, 165, 0);
    constexpr DWORD kActiveNoteTimeoutMs = 1200;
    constexpr DWORD kNoteGlowFadeMs = 220;

    using AllowDarkModeForWindowFn = BOOL(WINAPI*)(HWND, BOOL);

    void ApplyDarkThemeToControl(HWND hwnd)
    {
        static AllowDarkModeForWindowFn allowDarkModeForWindow = nullptr;
        static bool initialized = false;
        if (!initialized)
        {
            HMODULE module = LoadLibraryW(L"uxtheme.dll");
            if (module)
            {
                allowDarkModeForWindow = reinterpret_cast<AllowDarkModeForWindowFn>(
                    GetProcAddress(module, "AllowDarkModeForWindow"));
            }
            initialized = true;
        }

        if (allowDarkModeForWindow)
        {
            allowDarkModeForWindow(hwnd, TRUE);
        }
        SetWindowTheme(hwnd, L"DarkMode_Explorer", nullptr);
    }

    struct RandomGlowConfig
    {
        bool enabled = false;
        unsigned int seed = 1337;
        float intensity = 0.6f;
        float radius = 9.0f;
        float softness = 0.7f;
    };

    unsigned int HashColor(unsigned int seed, unsigned int index)
    {
        unsigned int value = seed ^ (index + 0x9e3779b9u);
        value ^= value >> 16;
        value *= 0x7feb352du;
        value ^= value >> 15;
        value *= 0x846ca68bu;
        value ^= value >> 16;
        return value;
    }

    Gdiplus::Color ColorFromHash(unsigned int hash, BYTE alpha)
    {
        BYTE r = static_cast<BYTE>(hash & 0xFF);
        BYTE g = static_cast<BYTE>((hash >> 8) & 0xFF);
        BYTE b = static_cast<BYTE>((hash >> 16) & 0xFF);
        return Gdiplus::Color(alpha, r, g, b);
    }

    Gdiplus::Color SoftenGlowColor(const Gdiplus::Color& color, BYTE alpha, float mix)
    {
        const float clampMix = std::clamp(mix, 0.0f, 1.0f);
        const BYTE target = 200;
        BYTE r = static_cast<BYTE>(color.GetR() * (1.0f - clampMix) + target * clampMix);
        BYTE g = static_cast<BYTE>(color.GetG() * (1.0f - clampMix) + target * clampMix);
        BYTE b = static_cast<BYTE>(color.GetB() * (1.0f - clampMix) + target * clampMix);
        return Gdiplus::Color(alpha, r, g, b);
    }

    Gdiplus::Color BoostTextColor(const Gdiplus::Color& color, BYTE boost)
    {
        const BYTE r = static_cast<BYTE>(std::min<int>(255, color.GetR() + boost));
        const BYTE g = static_cast<BYTE>(std::min<int>(255, color.GetG() + boost));
        const BYTE b = static_cast<BYTE>(std::min<int>(255, color.GetB() + boost));
        return Gdiplus::Color(color.GetA(), r, g, b);
    }

    bool IsIdentifierStart(wchar_t ch)
    {
        return (ch >= L'a' && ch <= L'z') || (ch >= L'A' && ch <= L'Z') || ch == L'_';
    }

    bool IsIdentifierChar(wchar_t ch)
    {
        return IsIdentifierStart(ch) || (ch >= L'0' && ch <= L'9');
    }

    bool IsIdentifierToken(const std::wstring& token)
    {
        if (token.empty())
        {
            return false;
        }
        for (wchar_t ch : token)
        {
            if (!IsIdentifierChar(ch))
            {
                return false;
            }
        }
        return true;
    }

    bool IsDigit(wchar_t ch)
    {
        return ch >= L'0' && ch <= L'9';
    }

    int ParseIntToken(const std::wstring& token)
    {
        if (token.empty())
        {
            return -1;
        }
        wchar_t* end = nullptr;
        long value = std::wcstol(token.c_str(), &end, 10);
        if (end == token.c_str())
        {
            return -1;
        }
        return static_cast<int>(value);
    }

    bool ParseBool(const std::string& text, const std::string& key, bool fallback)
    {
        size_t pos = text.find(key);
        if (pos == std::string::npos)
        {
            return fallback;
        }
        size_t valuePos = text.find(':', pos);
        if (valuePos == std::string::npos)
        {
            return fallback;
        }
        size_t start = text.find_first_not_of(" \t\r\n", valuePos + 1);
        if (start == std::string::npos)
        {
            return fallback;
        }
        if (text.compare(start, 4, "true") == 0)
        {
            return true;
        }
        if (text.compare(start, 5, "false") == 0)
        {
            return false;
        }
        return fallback;
    }

    unsigned int ParseUInt(const std::string& text, const std::string& key, unsigned int fallback)
    {
        size_t pos = text.find(key);
        if (pos == std::string::npos)
        {
            return fallback;
        }
        size_t valuePos = text.find(':', pos);
        if (valuePos == std::string::npos)
        {
            return fallback;
        }
        const char* start = text.c_str() + valuePos + 1;
        char* end = nullptr;
        unsigned long value = std::strtoul(start, &end, 10);
        if (start == end)
        {
            return fallback;
        }
        return static_cast<unsigned int>(value);
    }

    float ParseFloat(const std::string& text, const std::string& key, float fallback)
    {
        size_t pos = text.find(key);
        if (pos == std::string::npos)
        {
            return fallback;
        }
        size_t valuePos = text.find(':', pos);
        if (valuePos == std::string::npos)
        {
            return fallback;
        }
        const char* start = text.c_str() + valuePos + 1;
        char* end = nullptr;
        float value = std::strtof(start, &end);
        if (start == end)
        {
            return fallback;
        }
        return value;
    }

    RandomGlowConfig ParseRandomGlow(const std::string& text)
    {
        RandomGlowConfig config;
        size_t section = text.find("\"randomCharGlowTest\"");
        if (section == std::string::npos)
        {
            return config;
        }

        std::string slice = text.substr(section, 512);
        config.enabled = ParseBool(slice, "\"enabled\"", false);
        config.seed = ParseUInt(slice, "\"seed\"", config.seed);
        config.intensity = ParseFloat(slice, "\"intensity\"", config.intensity);
        config.radius = ParseFloat(slice, "\"radius\"", config.radius);
        config.softness = ParseFloat(slice, "\"softness\"", config.softness);
        return config;
    }

    bool IsWhitespaceOnly(const std::wstring& text, size_t start, size_t end)
    {
        for (size_t i = start; i < end && i < text.size(); ++i)
        {
            if (!iswspace(text[i]))
            {
                return false;
            }
        }
        return true;
    }

    bool IsWhitespaceOnlyLine(const std::wstring& line)
    {
        return IsWhitespaceOnly(line, 0, line.size());
    }

    bool TryFindFoldBraceForLine(const std::wstring& text, int lineStart, int lineLength, int& bracePos, bool& braceOnNextLine)
    {
        braceOnNextLine = false;
        if (lineStart < 0 || lineLength <= 0 || lineStart >= static_cast<int>(text.size()))
        {
            return false;
        }

        size_t start = static_cast<size_t>(lineStart);
        size_t end = std::min(text.size(), start + static_cast<size_t>(lineLength));
        size_t pos = text.find(L'{', start);
        if (pos != std::wstring::npos && pos < end)
        {
            bracePos = static_cast<int>(pos);
            return true;
        }

        size_t scan = text.find(L'\n', start);
        if (scan == std::wstring::npos)
        {
            return false;
        }
        scan += 1;

        while (scan < text.size())
        {
            size_t lineEnd = text.find(L'\n', scan);
            if (lineEnd == std::wstring::npos)
            {
                lineEnd = text.size();
            }

            size_t first = scan;
            while (first < lineEnd && iswspace(text[first]))
            {
                ++first;
            }

            if (first >= lineEnd)
            {
                scan = lineEnd + 1;
                continue;
            }

            if (text[first] == L'{')
            {
                bracePos = static_cast<int>(first);
                braceOnNextLine = true;
                return true;
            }

            return false;
        }

        return false;
    }

    bool TryFindMatchingBrace(const std::wstring& text, int bracePos, int& matchPos)
    {
        if (bracePos < 0 || bracePos >= static_cast<int>(text.size()))
        {
            return false;
        }

        int depth = 1;
        for (size_t i = static_cast<size_t>(bracePos + 1); i < text.size(); ++i)
        {
            wchar_t ch = text[i];
            if (ch == L'{')
            {
                ++depth;
            }
            else if (ch == L'}')
            {
                --depth;
                if (depth == 0)
                {
                    matchPos = static_cast<int>(i);
                    return true;
                }
            }
        }

        return false;
    }

    bool TryGetFoldMarkerId(const std::wstring& text, int bracePos, std::wstring& outId)
    {
        if (bracePos < 0 || bracePos >= static_cast<int>(text.size()))
        {
            return false;
        }

        size_t searchStart = static_cast<size_t>(bracePos);
        size_t markerPos = text.find(kFoldMarkerPrefix, searchStart);
        if (markerPos == std::wstring::npos)
        {
            return false;
        }

        size_t idStart = markerPos + wcslen(kFoldMarkerPrefix);
        size_t idEnd = text.find(L"*/", idStart);
        if (idEnd == std::wstring::npos)
        {
            return false;
        }

        outId = text.substr(idStart, idEnd - idStart);
        return !outId.empty();
    }

    bool IsBraceOnlyLine(const std::wstring& line, size_t bracePos)
    {
        if (bracePos >= line.size())
        {
            return false;
        }

        for (size_t i = 0; i < bracePos; ++i)
        {
            if (!iswspace(line[i]))
            {
                return false;
            }
        }

        size_t i = bracePos + 1;
        while (i < line.size() && iswspace(line[i]))
        {
            ++i;
        }
        if (i >= line.size())
        {
            return true;
        }
        if (line[i] == L'/' && (i + 1) < line.size() && line[i + 1] == L'/')
        {
            return true;
        }
        return false;
    }
}

void EditorView::Initialize(HWND parent)
{
    _parent = parent;
    _bgBrush = CreateSolidBrush(kBgColor);
    Gdiplus::GdiplusStartupInput gdiplusStartupInput{};
    Gdiplus::GdiplusStartup(&_gdiplusToken, &gdiplusStartupInput, nullptr);
    _font = CreateFontW(
        20, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_MODERN, L"Consolas");

    _editor = CreateWindowExW(
        0,
        L"EDIT",
        L"// MusicEngine Editor\n// Write your MusicEngine script here and press Play.\n\n",
        WS_CHILD | WS_VISIBLE | WS_VSCROLL | WS_HSCROLL |
            ES_LEFT | ES_MULTILINE | ES_AUTOVSCROLL | ES_AUTOHSCROLL,
        0, 0, 0, 0,
        parent,
        nullptr,
        reinterpret_cast<HINSTANCE>(GetWindowLongPtr(parent, GWLP_HINSTANCE)),
        nullptr);

    if (_editor)
    {
        ApplyDarkThemeToControl(_editor);
        SendMessageW(_editor, WM_SETFONT, reinterpret_cast<WPARAM>(_font), TRUE);
        SetWindowLongPtrW(_editor, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(this));
        _originalEditProc = reinterpret_cast<WNDPROC>(SetWindowLongPtrW(_editor, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(EditProc)));

        LOGFONTW logFont{};
        GetObjectW(_font, sizeof(logFont), &logFont);
        HDC hdc = GetDC(_editor);
        if (hdc)
        {
            _gdiFont = new Gdiplus::Font(hdc, &logFont);
            ReleaseDC(_editor, hdc);
        }
    }

    const wchar_t* renderClass = L"MusicEngineEditorRender";
    WNDCLASSW renderClassDef{};
    renderClassDef.lpfnWndProc = EditorView::RenderProc;
    renderClassDef.hInstance = reinterpret_cast<HINSTANCE>(GetWindowLongPtr(parent, GWLP_HINSTANCE));
    renderClassDef.lpszClassName = renderClass;
    RegisterClassW(&renderClassDef);

    _render = CreateWindowExW(
        WS_EX_TRANSPARENT,
        renderClass,
        L"",
        WS_CHILD | WS_VISIBLE,
        0, 0, 0, 0,
        parent,
        nullptr,
        reinterpret_cast<HINSTANCE>(GetWindowLongPtr(parent, GWLP_HINSTANCE)),
        this);

    UpdateCharMetrics();
    LoadVisualConfig();

    Resize(0, 0);
}

void EditorView::Resize(int topOffset, int bottomOffset)
{
    if (!_editor || !_parent)
    {
        return;
    }

    UpdateGutterWidth();

    RECT prevGutter = _lastGutterRect;
    RECT rect{};
    GetClientRect(_parent, &rect);
    const int width = rect.right - rect.left;
    const int height = rect.bottom - rect.top;
    const int x = _gutterWidth + kGutterGap;
    const int y = kEditorPadding + topOffset;
    const int w = width - kEditorPadding - _gutterWidth - kGutterGap;
    const int h = height - (kEditorPadding * 2) - topOffset - bottomOffset;
    MoveWindow(_editor, x, y, w, h, TRUE);
    if (_render)
    {
        MoveWindow(_render, x, y, w, h, TRUE);
        SetWindowPos(_render, _editor, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    RECT newGutter{};
    if (GetGutterRect(newGutter))
    {
        _lastGutterRect = newGutter;
        RECT invalidate{};
        if (!IsRectEmpty(&prevGutter))
        {
            UnionRect(&invalidate, &prevGutter, &newGutter);
        }
        else
        {
            invalidate = newGutter;
        }
        InvalidateRect(_parent, &invalidate, TRUE);
    }
}

void EditorView::Shutdown()
{
    if (_render)
    {
        DestroyWindow(_render);
        _render = nullptr;
    }
    if (_gdiFont)
    {
        delete _gdiFont;
        _gdiFont = nullptr;
    }
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
    if (_gdiplusToken != 0)
    {
        Gdiplus::GdiplusShutdown(_gdiplusToken);
        _gdiplusToken = 0;
    }
}

LRESULT EditorView::OnEditColor(HDC hdc)
{
    bool hasSelection = false;
    if (_editor)
    {
        DWORD start = 0;
        DWORD end = 0;
        SendMessageW(_editor, EM_GETSEL, reinterpret_cast<WPARAM>(&start), reinterpret_cast<LPARAM>(&end));
        hasSelection = start != end;
    }
    SetTextColor(hdc, (_syntaxOverlayEnabled && !hasSelection) ? kBgColor : kTextColor);
    SetBkColor(hdc, kBgColor);
    return reinterpret_cast<LRESULT>(_bgBrush);
}

void EditorView::DrawLineNumbers(HDC hdc)
{
    if (!_parent || !_editor || !hdc)
    {
        return;
    }

    const int savedState = SaveDC(hdc);
    SelectClipRgn(hdc, nullptr);

    RECT gutter{};
    if (!GetGutterRect(gutter))
    {
        return;
    }

    HBRUSH gutterBrush = CreateSolidBrush(kGutterColor);
    FillRect(hdc, &gutter, gutterBrush);
    DeleteObject(gutterBrush);

    int firstLine = static_cast<int>(SendMessageW(_editor, EM_GETFIRSTVISIBLELINE, 0, 0));
    int totalLines = GetLineCount();
    int visibleLines = _lineHeight > 0 ? (gutter.bottom - gutter.top) / _lineHeight + 1 : 0;
    std::wstring fullText = GetText();

    HFONT oldFont = _font ? static_cast<HFONT>(SelectObject(hdc, _font)) : nullptr;
    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, kGutterTextColor);
    HPEN iconPen = CreatePen(PS_SOLID, 1, kGutterTextColor);
    HBRUSH iconBrush = CreateSolidBrush(kGutterTextColor);
    HPEN oldPen = iconPen ? static_cast<HPEN>(SelectObject(hdc, iconPen)) : nullptr;
    HBRUSH oldBrush = iconBrush ? static_cast<HBRUSH>(SelectObject(hdc, iconBrush)) : nullptr;

    for (int i = 0; i < visibleLines; ++i)
    {
        int lineNo = firstLine + i + 1;
        if (lineNo > totalLines)
        {
            break;
        }

        int y = gutter.top + (i * _lineHeight);
        RECT lineRect{ gutter.left + kGutterTextPadding, y, gutter.right - kFoldIconAreaWidth - 2, y + _lineHeight };
        std::wstring text = std::to_wstring(lineNo);
        DrawTextW(hdc, text.c_str(), static_cast<int>(text.size()), &lineRect,
            DT_LEFT | DT_VCENTER | DT_SINGLELINE);

        bool foldable = false;
        bool folded = false;
        if (!fullText.empty())
        {
            int lineStart = static_cast<int>(SendMessageW(_editor, EM_LINEINDEX, lineNo - 1, 0));
            if (lineStart >= 0)
            {
                int lineLength = static_cast<int>(SendMessageW(_editor, EM_LINELENGTH, lineStart, 0));
                std::wstring line;
                if (lineLength > 0 && lineStart + lineLength <= static_cast<int>(fullText.size()))
                {
                    line = fullText.substr(static_cast<size_t>(lineStart), static_cast<size_t>(lineLength));
                }
                int bracePos = -1;
                int matchPos = -1;
                bool braceOnNextLine = false;
                if (!line.empty() && !IsWhitespaceOnlyLine(line) &&
                    TryFindFoldBraceForLine(fullText, lineStart, lineLength, bracePos, braceOnNextLine) &&
                    TryFindMatchingBrace(fullText, bracePos, matchPos))
                {
                    size_t newline = fullText.find(L'\n', static_cast<size_t>(bracePos));
                    foldable = newline != std::wstring::npos && static_cast<int>(newline) < matchPos;
                    std::wstring id;
                    folded = TryGetFoldMarkerId(fullText, bracePos, id);
                    if (foldable && !braceOnNextLine && bracePos >= lineStart && bracePos < lineStart + lineLength)
                    {
                        size_t localPos = static_cast<size_t>(bracePos - lineStart);
                        if (localPos < line.size() && IsBraceOnlyLine(line, localPos))
                        {
                            foldable = false;
                            folded = false;
                        }
                    }
                }
            }
        }

        if (foldable)
        {
            int centerY = y + (_lineHeight / 2);
            int iconLeft = gutter.right - kFoldIconAreaWidth + (kFoldIconAreaWidth - kFoldIconSize) / 2;
            if (folded)
            {
                POINT pts[3] = {
                    { iconLeft, centerY - (kFoldIconSize / 2) },
                    { iconLeft, centerY + (kFoldIconSize / 2) },
                    { iconLeft + kFoldIconSize, centerY }
                };
                Polygon(hdc, pts, 3);
            }
            else
            {
                POINT pts[3] = {
                    { iconLeft, centerY - (kFoldIconSize / 2) },
                    { iconLeft + kFoldIconSize, centerY - (kFoldIconSize / 2) },
                    { iconLeft + (kFoldIconSize / 2), centerY + (kFoldIconSize / 2) }
                };
                Polygon(hdc, pts, 3);
            }
        }
    }

    if (oldFont)
    {
        SelectObject(hdc, oldFont);
    }
    if (oldPen)
    {
        SelectObject(hdc, oldPen);
    }
    if (oldBrush)
    {
        SelectObject(hdc, oldBrush);
    }
    if (iconPen)
    {
        DeleteObject(iconPen);
    }
    if (iconBrush)
    {
        DeleteObject(iconBrush);
    }

    if (savedState != 0)
    {
        RestoreDC(hdc, savedState);
    }
}

bool EditorView::HandleGutterClick(POINT pt)
{
    if (!_editor || !_parent)
    {
        return false;
    }

    RECT gutter{};
    if (!GetGutterRect(gutter) || !PtInRect(&gutter, pt))
    {
        return false;
    }

    int lineHeight = _lineHeight > 0 ? _lineHeight : 1;
    int firstLine = static_cast<int>(SendMessageW(_editor, EM_GETFIRSTVISIBLELINE, 0, 0));
    int offsetY = static_cast<int>(pt.y) - static_cast<int>(gutter.top);
    int lineIndex = firstLine + std::max<int>(0, offsetY / lineHeight);
    int totalLines = GetLineCount();
    if (lineIndex >= totalLines)
    {
        lineIndex = totalLines - 1;
    }
    if (lineIndex < 0)
    {
        return true;
    }

    int start = static_cast<int>(SendMessageW(_editor, EM_LINEINDEX, lineIndex, 0));
    if (start < 0)
    {
        return true;
    }

    if (pt.x >= gutter.right - kFoldIconAreaWidth)
    {
        if (ToggleFoldAtLine(lineIndex))
        {
            return true;
        }
    }

    int end = start;
    if (lineIndex + 1 < totalLines)
    {
        int next = static_cast<int>(SendMessageW(_editor, EM_LINEINDEX, lineIndex + 1, 0));
        if (next > start)
        {
            end = next;
        }
    }
    if (end == start)
    {
        int length = static_cast<int>(SendMessageW(_editor, EM_LINELENGTH, start, 0));
        end = start + std::max(0, length);
    }

    SetFocus(_editor);
    SendMessageW(_editor, EM_SETSEL, start, end);
    return true;
}

bool EditorView::GetGutterRect(RECT& outRect) const
{
    if (!_editor || !_parent)
    {
        return false;
    }

    RECT editRect{};
    GetWindowRect(_editor, &editRect);
    POINT topLeft{ editRect.left, editRect.top };
    POINT bottomRight{ editRect.right, editRect.bottom };
    ScreenToClient(_parent, &topLeft);
    ScreenToClient(_parent, &bottomRight);
    outRect = RECT{
        0,
        topLeft.y,
        _gutterWidth,
        bottomRight.y
    };
    return true;
}

bool EditorView::ToggleFoldAtLine(int lineIndex)
{
    if (!_editor)
    {
        return false;
    }

    std::wstring fullText = GetText();
    if (fullText.empty())
    {
        return false;
    }

    int lineStart = static_cast<int>(SendMessageW(_editor, EM_LINEINDEX, lineIndex, 0));
    if (lineStart < 0)
    {
        return false;
    }

    int lineLength = static_cast<int>(SendMessageW(_editor, EM_LINELENGTH, lineStart, 0));
    if (lineLength <= 0 || lineStart + lineLength > static_cast<int>(fullText.size()))
    {
        return false;
    }

    int bracePos = -1;
    int matchPos = -1;
    bool braceOnNextLine = false;
    if (!TryFindFoldBraceForLine(fullText, lineStart, lineLength, bracePos, braceOnNextLine))
    {
        return false;
    }

    std::wstring line = fullText.substr(static_cast<size_t>(lineStart), static_cast<size_t>(lineLength));
    if (!braceOnNextLine)
    {
        size_t localPos = static_cast<size_t>(bracePos - lineStart);
        if (localPos < line.size() && IsBraceOnlyLine(line, localPos))
        {
            return false;
        }
    }

    std::wstring foldedId;
    if (TryGetFoldMarkerId(fullText, bracePos, foldedId))
    {
        auto it = _foldedBlocks.find(foldedId);
        if (it != _foldedBlocks.end())
        {
            std::wstring marker = L"{ ";
            marker += kFoldMarkerPrefix;
            marker += foldedId;
            marker += L"*/ }";
            size_t markerPosText = fullText.find(marker, static_cast<size_t>(bracePos));
            if (markerPosText != std::wstring::npos)
            {
                fullText.replace(markerPosText, marker.size(), it->second);
                _foldedBlocks.erase(it);
                SetWindowTextW(_editor, fullText.c_str());
                InvalidateRect(_parent, nullptr, TRUE);
                return true;
            }
        }
        return false;
    }

    if (!TryFindMatchingBrace(fullText, bracePos, matchPos))
    {
        return false;
    }

    size_t newline = fullText.find(L'\n', static_cast<size_t>(bracePos));
    if (newline == std::wstring::npos || static_cast<int>(newline) >= matchPos)
    {
        return false;
    }

    std::wstring id = L"F" + std::to_wstring(++_foldCounter);
    std::wstring original = fullText.substr(static_cast<size_t>(bracePos),
        static_cast<size_t>(matchPos - bracePos + 1));
    std::wstring marker = L"{ ";
    marker += kFoldMarkerPrefix;
    marker += id;
    marker += L"*/ }";

    fullText.replace(static_cast<size_t>(bracePos), static_cast<size_t>(matchPos - bracePos + 1), marker);
    _foldedBlocks[id] = std::move(original);
    SetWindowTextW(_editor, fullText.c_str());
    InvalidateRect(_parent, nullptr, TRUE);
    return true;
}

void EditorView::RenderOverlay(HDC hdc, int width, int height)
{
    if (width <= 0 || height <= 0)
    {
        return;
    }

    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = width;
    bmi.bmiHeader.biHeight = -height;
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    void* bits = nullptr;
    HBITMAP dib = CreateDIBSection(hdc, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!dib)
    {
        return;
    }

    HDC memDC = CreateCompatibleDC(hdc);
    HBITMAP oldBmp = static_cast<HBITMAP>(SelectObject(memDC, dib));
    if (bits)
    {
        std::memset(bits, 0, static_cast<size_t>(width) * static_cast<size_t>(height) * 4);
    }

    Gdiplus::Graphics graphics(memDC);
    DrawCustomTextToGraphics(graphics, width, height);

    if (_editor)
    {
        HDC editorDC = GetDC(_editor);
        if (editorDC)
        {
            BitBlt(hdc, 0, 0, width, height, editorDC, 0, 0, SRCCOPY);
            ReleaseDC(_editor, editorDC);
        }
    }

    BLENDFUNCTION blend{};
    blend.BlendOp = AC_SRC_OVER;
    blend.SourceConstantAlpha = 255;
    blend.AlphaFormat = AC_SRC_ALPHA;
    AlphaBlend(hdc, 0, 0, width, height, memDC, 0, 0, width, height, blend);

    SelectObject(memDC, oldBmp);
    DeleteObject(dib);
    DeleteDC(memDC);
}

void EditorView::DrawCustomTextToGraphics(Gdiplus::Graphics& graphics, int width, int height)
{
    if (!_parent || !_editor || !_syntaxOverlayEnabled)
    {
        return;
    }

    DWORD selStart = 0;
    DWORD selEnd = 0;
    SendMessageW(_editor, EM_GETSEL, reinterpret_cast<WPARAM>(&selStart), reinterpret_cast<LPARAM>(&selEnd));
    if (selStart != selEnd)
    {
        return;
    }

    int firstLine = static_cast<int>(SendMessageW(_editor, EM_GETFIRSTVISIBLELINE, 0, 0));
    int totalLines = GetLineCount();
    int visibleLines = _lineHeight > 0 ? height / _lineHeight + 1 : 0;

    graphics.SetSmoothingMode(Gdiplus::SmoothingModeHighQuality);
    graphics.SetCompositingQuality(Gdiplus::CompositingQualityHighQuality);
    graphics.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBicubic);
    graphics.SetPixelOffsetMode(Gdiplus::PixelOffsetModeHighQuality);

    if (!_gdiFont)
    {
        LOGFONTW logFont{};
        if (_font)
        {
            GetObjectW(_font, sizeof(logFont), &logFont);
        }
        HDC fontHdc = GetDC(_editor);
        if (fontHdc)
        {
            _gdiFont = new Gdiplus::Font(fontHdc, &logFont);
            ReleaseDC(_editor, fontHdc);
        }
    }
    if (!_gdiFont)
    {
        return;
    }

    const int visibleColumns = _charWidth > 0 ? width / _charWidth + 1 : 0;
    if (visibleColumns <= 0 || _lineHeight <= 0)
    {
        return;
    }
    int scrollX = GetScrollPos(_editor, SB_HORZ);
    if (scrollX < 0)
    {
        scrollX = 0;
    }
    const int lineWidth = visibleColumns * _charWidth;

    const DWORD now = GetTickCount();
    bool hasGlow = false;
    for (const auto& pair : _noteGlow)
    {
        const auto& state = pair.second;
        if (state.active)
        {
            hasGlow = true;
            break;
        }
        if (state.lastOffTick != 0 && static_cast<int>(now - state.lastOffTick) >= 0 &&
            (now - state.lastOffTick) < kNoteGlowFadeMs)
        {
            hasGlow = true;
            break;
        }
    }
    const bool allowCache = !hasGlow;
    bool cacheInvalid =
        _cacheGlowSeed != _randomGlowSeed ||
        _cacheGlowIntensity != _randomGlowIntensity ||
        _cacheGlowRadius != _randomGlowRadius ||
        _cacheGlowSoftness != _randomGlowSoftness ||
        _cacheColumns != visibleColumns ||
        _cacheLineHeight != _lineHeight ||
        _cacheScrollX != scrollX ||
        _cacheActiveNotesVersion != _activeNotesVersion;

    if (cacheInvalid || !allowCache)
    {
        _lineCache.clear();
        _cacheGlowSeed = _randomGlowSeed;
        _cacheGlowIntensity = _randomGlowIntensity;
        _cacheGlowRadius = _randomGlowRadius;
        _cacheGlowSoftness = _randomGlowSoftness;
        _cacheColumns = visibleColumns;
        _cacheLineHeight = _lineHeight;
        _cacheScrollX = scrollX;
        _cacheActiveNotesVersion = _activeNotesVersion;
    }

    const int glowLayers = 3;
    const int glowStep = 1;
    const BYTE glowAlphaBase = 40;

    for (int lineIndex = 0; lineIndex < visibleLines; ++lineIndex)
    {
        int lineNo = firstLine + lineIndex;
        if (lineNo >= totalLines)
        {
            break;
        }

        int lineLength = static_cast<int>(SendMessageW(_editor, EM_LINELENGTH,
            static_cast<WPARAM>(SendMessageW(_editor, EM_LINEINDEX, lineNo, 0)), 0));
        if (lineLength < 0)
        {
            continue;
        }

        std::wstring line(static_cast<size_t>(lineLength) + 1, L'\0');
        *reinterpret_cast<WORD*>(&line[0]) = static_cast<WORD>(lineLength + 1);
        int copied = static_cast<int>(SendMessageW(_editor, EM_GETLINE, lineNo,
            reinterpret_cast<LPARAM>(line.data())));
        line.resize(static_cast<size_t>(std::max(0, copied)));

        const int y = lineIndex * _lineHeight;
        auto& entry = _lineCache[lineNo];
        bool needsRender = !allowCache || entry.text != line || !entry.bitmap || entry.width != lineWidth || entry.height != _lineHeight;

        if (needsRender)
        {
            entry.text = line;
            entry.width = lineWidth;
            entry.height = _lineHeight;
            entry.bitmap = std::make_unique<Gdiplus::Bitmap>(lineWidth, _lineHeight, PixelFormat32bppPARGB);

            Gdiplus::Graphics lineGraphics(entry.bitmap.get());
            lineGraphics.SetTextRenderingHint(Gdiplus::TextRenderingHintAntiAliasGridFit);
            lineGraphics.SetSmoothingMode(Gdiplus::SmoothingModeHighQuality);
            lineGraphics.SetCompositingQuality(Gdiplus::CompositingQualityHighQuality);
            lineGraphics.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBicubic);
            lineGraphics.SetPixelOffsetMode(Gdiplus::PixelOffsetModeHighQuality);
            lineGraphics.Clear(Gdiplus::Color(0, 0, 0, 0));

            Gdiplus::SolidBrush glowBrush(Gdiplus::Color(0, 0, 0, 0));
            Gdiplus::SolidBrush textBrush(Gdiplus::Color(255, 255, 255, 255));
            std::vector<Gdiplus::Color> colors;
            std::vector<float> glow;
            BuildSyntaxColors(line, colors, glow, now);
            if (static_cast<size_t>(scrollX) >= line.size())
            {
                lineGraphics.Clear(Gdiplus::Color(0, 0, 0, 0));
            }
            const size_t startColumn = std::min<size_t>(line.size(), static_cast<size_t>(scrollX));
            const size_t maxColumn = std::min<size_t>(line.size(), startColumn + static_cast<size_t>(visibleColumns));
            for (size_t col = startColumn; col < maxColumn; ++col)
            {
                wchar_t ch = line[col];
                if (ch == L'\r' || ch == L'\n')
                {
                    continue;
                }

                Gdiplus::Color textColor = col < colors.size() ? colors[col] : kDefaultText;
                Gdiplus::Color glowColor = SoftenGlowColor(textColor, glowAlphaBase, 0.55f);

                float x = static_cast<float>(static_cast<int>(col - startColumn) * _charWidth);
                textBrush.SetColor(textColor);
                float glowIntensity = (col < glow.size()) ? glow[col] : 0.0f;
                if (glowIntensity > 0.0f)
                {
                    glowBrush.SetColor(glowColor);
                    for (int layer = 1; layer <= glowLayers; ++layer)
                    {
                        const int offset = glowStep * layer;
                        const int baseAlpha = glowAlphaBase - (layer - 1) * (glowAlphaBase / glowLayers);
                        const BYTE layerAlpha = static_cast<BYTE>(std::max(0, static_cast<int>(baseAlpha * glowIntensity)));
                        glowBrush.SetColor(Gdiplus::Color(layerAlpha, glowColor.GetR(), glowColor.GetG(), glowColor.GetB()));
                        const int radiusSq = offset * offset;
                        for (int dx = -offset; dx <= offset; ++dx)
                        {
                            for (int dy = -offset; dy <= offset; ++dy)
                            {
                                if (dx == 0 && dy == 0)
                                {
                                    continue;
                                }
                                if ((dx * dx + dy * dy) > radiusSq)
                                {
                                    continue;
                                }
                                Gdiplus::PointF pos(x + dx, static_cast<float>(dy));
                                wchar_t buffer[2]{ ch, L'\0' };
                                lineGraphics.DrawString(buffer, 1, _gdiFont, pos, &glowBrush);
                            }
                        }
                    }
                }

                Gdiplus::PointF pos(x, 0.0f);
                wchar_t buffer[2]{ ch, L'\0' };
                lineGraphics.DrawString(buffer, 1, _gdiFont, pos, &textBrush);
            }
        }

        if (entry.bitmap)
        {
            graphics.DrawImage(entry.bitmap.get(), 0, y);
        }
    }
}

void EditorView::BuildSyntaxColors(const std::wstring& line, std::vector<Gdiplus::Color>& colors,
    std::vector<float>& glow, DWORD now) const
{
    colors.assign(line.size(), kDefaultText);
    glow.assign(line.size(), 0.0f);

    if (line.empty())
    {
        return;
    }

    auto applyColor = [&](size_t start, size_t end, const Gdiplus::Color& color)
    {
        if (start >= end || start >= line.size())
        {
            return;
        }
        const size_t clampedEnd = std::min(end, line.size());
        for (size_t i = start; i < clampedEnd; ++i)
        {
            colors[i] = color;
        }
    };

    bool inString = false;
    int parenDepth = 0;
    bool noteCallPending = false;
    bool inNoteCall = false;
    bool noteArgCaptured = false;
    bool pendingVstName = false;
    bool vstStringActive = false;

    for (size_t i = 0; i < line.size();)
    {
        if (!inString && i + 1 < line.size() && line[i] == L'/' && line[i + 1] == L'/')
        {
            applyColor(i, line.size(), kCommentGray);
            break;
        }

        wchar_t ch = line[i];
        if (!inString && pendingVstName)
        {
            if (!iswspace(ch) && ch != L'"' && ch != L'(' && ch != L',' && ch != L')')
            {
                pendingVstName = false;
            }
        }

        if (ch == L'"')
        {
            if (!inString)
            {
                inString = true;
                vstStringActive = pendingVstName;
                pendingVstName = false;
            }
            else
            {
                inString = false;
                if (vstStringActive)
                {
                    vstStringActive = false;
                }
            }

            ++i;
            continue;
        }

        if (inString)
        {
            if (vstStringActive)
            {
                applyColor(i, i + 1, kVstNameOrange);
            }
            ++i;
            continue;
        }

        if (!inString)
        {
            if (ch == L'(')
            {
                ++parenDepth;
            }
            else if (ch == L')')
            {
                parenDepth = std::max(0, parenDepth - 1);
                if (parenDepth == 0)
                {
                    inNoteCall = false;
                    noteArgCaptured = false;
                }
            }
        }

        if (!inString && IsIdentifierStart(ch))
        {
            size_t start = i;
            size_t end = i + 1;
            while (end < line.size() && IsIdentifierChar(line[end]))
            {
                ++end;
            }
            std::wstring token = line.substr(start, end - start);

            if (token == L"var")
            {
                applyColor(start, end, kKeywordBlue);
            }
            else if (token == L"true")
            {
                applyColor(start, end, kBoolTrueGreen);
            }
            else if (token == L"false")
            {
                applyColor(start, end, kBoolFalseRed);
            }
            else if (token == L"CreatePattern")
            {
                applyColor(start, end, kMethodGreen);
            }
            else if (token == L"CreateSynth")
            {
                applyColor(start, end, kMethodGreen);
            }
            else if (token == L"CreateVst")
            {
                applyColor(start, end, kMethodGreen);
                pendingVstName = true;
            }
            else if (token == L"pattern")
            {
                applyColor(start, end, kPatternPink);
            }
            else if (token == L"Note" && noteCallPending)
            {
                applyColor(start, end, kMethodGreen);
                inNoteCall = true;
                noteArgCaptured = false;
                noteCallPending = false;
            }
            else if (token == L"synth")
            {
                applyColor(start, end, kNameTurquoise);
            }
            else
            {
                applyColor(start, end, kNameTurquoise);
            }

            size_t look = end;
            while (look < line.size() && iswspace(line[look]))
            {
                ++look;
            }
            if (look < line.size() && line[look] == L'.')
            {
                size_t idStart = look + 1;
                while (idStart < line.size() && iswspace(line[idStart]))
                {
                    ++idStart;
                }
                size_t idEnd = idStart;
                while (idEnd < line.size() && IsIdentifierChar(line[idEnd]))
                {
                    ++idEnd;
                }
                std::wstring method = line.substr(idStart, idEnd - idStart);
                if (method == L"Note")
                {
                    applyColor(start, end, kPatternWhite);
                    if (look < line.size())
                    {
                        colors[look] = kPatternWhite;
                    }
                    noteCallPending = true;
                }
            }

            i = end;
            continue;
        }

        if (!inString && IsDigit(ch))
        {
            size_t start = i;
            size_t end = i + 1;
            while (end < line.size() && (IsDigit(line[end]) || line[end] == L'.'))
            {
                ++end;
            }
            if (parenDepth > 0)
            {
                applyColor(start, end, kNumberPink);
            }

            if (inNoteCall && !noteArgCaptured && parenDepth > 0)
            {
                int noteValue = ParseIntToken(line.substr(start, end - start));
                float intensity = 0.0f;
                auto it = _noteGlow.find(noteValue);
                if (it != _noteGlow.end())
                {
                    const NoteGlowState& state = it->second;
                    if (state.active)
                    {
                        intensity = 1.0f;
                    }
                    else if (state.lastOffTick != 0 && static_cast<int>(now - state.lastOffTick) >= 0)
                    {
                        const DWORD elapsed = now - state.lastOffTick;
                        if (elapsed < kNoteGlowFadeMs)
                        {
                            intensity = 1.0f - (static_cast<float>(elapsed) / static_cast<float>(kNoteGlowFadeMs));
                        }
                    }
                }
                if (intensity > 0.0f)
                {
                    for (size_t idx = start; idx < end && idx < glow.size(); ++idx)
                    {
                        glow[idx] = std::max(glow[idx], intensity);
                    }
                }
                noteArgCaptured = true;
            }

            i = end;
            continue;
        }

        ++i;
    }
}

void EditorView::SetActiveNote(int note, bool active)
{
    if (note < 0)
    {
        return;
    }

    const DWORD now = GetTickCount();
    bool changed = false;
    if (active)
    {
        auto& state = _noteGlow[note];
        state.active = true;
        state.lastOnTick = now;
        state.lastOffTick = 0;
        changed = true;
    }
    else
    {
        auto it = _noteGlow.find(note);
        if (it != _noteGlow.end())
        {
            it->second.active = false;
            it->second.lastOffTick = now;
            changed = true;
        }
    }

    if (changed)
    {
        ++_activeNotesVersion;
        _lineCache.clear();
        if (_render)
        {
            RedrawWindow(_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
        }
    }
}

bool EditorView::PruneExpiredNotes()
{
    if (_noteGlow.empty())
    {
        return false;
    }

    const DWORD now = GetTickCount();
    bool changed = false;
    bool needsRedraw = false;
    for (auto it = _noteGlow.begin(); it != _noteGlow.end();)
    {
        NoteGlowState& state = it->second;
        if (state.active && static_cast<int>(now - state.lastOnTick) >= 0 &&
            (now - state.lastOnTick) >= kActiveNoteTimeoutMs)
        {
            state.active = false;
            state.lastOffTick = now;
            changed = true;
        }

        if (!state.active && state.lastOffTick != 0 &&
            static_cast<int>(now - state.lastOffTick) >= 0 &&
            (now - state.lastOffTick) >= kNoteGlowFadeMs)
        {
            it = _noteGlow.erase(it);
            changed = true;
            continue;
        }

        if (state.active ||
            (state.lastOffTick != 0 && static_cast<int>(now - state.lastOffTick) >= 0 &&
                (now - state.lastOffTick) < kNoteGlowFadeMs))
        {
            needsRedraw = true;
        }
        ++it;
    }

    if (changed)
    {
        ++_activeNotesVersion;
        _lineCache.clear();
    }

    if ((changed || needsRedraw) && _render)
    {
        RedrawWindow(_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
    }

    return changed || needsRedraw;
}

LRESULT CALLBACK EditorView::EditProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    EditorView* editor = reinterpret_cast<EditorView*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    HWND parent = GetParent(hwnd);

    if (editor)
    {
        auto invalidateGutter = [editor, parent](bool immediate)
        {
            if (!editor->_editor || !parent)
            {
                return;
            }
            RECT gutter{};
            if (!editor->GetGutterRect(gutter))
            {
                return;
            }
            UINT flags = RDW_INVALIDATE | RDW_NOERASE;
            if (immediate)
            {
                flags = RDW_INVALIDATE | RDW_ERASE | RDW_UPDATENOW;
            }
            RedrawWindow(parent, &gutter, nullptr, flags);
        };

        switch (msg)
        {
        case WM_ERASEBKGND:
            return 1;
        case WM_PAINT:
        {
            if (editor->_originalEditProc)
            {
                return CallWindowProcW(editor->_originalEditProc, hwnd, msg, wParam, lParam);
            }
            break;
        }
        case WM_KEYDOWN:
            if (wParam == VK_RETURN && (GetKeyState(VK_CONTROL) & 0x8000))
            {
                PostMessageW(parent, WM_COMMAND, MAKEWPARAM(kCommandRefresh, 0), 0);
            }
            if (wParam == VK_ESCAPE)
            {
                PostMessageW(parent, WM_COMMAND, MAKEWPARAM(kCommandStop, 0), 0);
                return 0;
            }
            if (editor->_render)
            {
                RedrawWindow(editor->_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
            }
            break;
        case WM_VSCROLL:
        case WM_MOUSEWHEEL:
            invalidateGutter(true);
            if (editor->_render)
            {
                RedrawWindow(editor->_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
            }
            break;
        case WM_KEYUP:
            if (wParam == VK_RETURN || wParam == VK_BACK || wParam == VK_DELETE)
            {
                invalidateGutter(false);
            }
            if (editor->_render)
            {
                RedrawWindow(editor->_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
            }
            break;
        case WM_CHAR:
            if ((wParam == L'\r' || wParam == L'\n') && (GetKeyState(VK_CONTROL) & 0x8000))
            {
                return 0;
            }
            if (wParam == L'\r' || wParam == L'\n')
            {
                invalidateGutter(false);
            }
            if (editor->_render)
            {
                RedrawWindow(editor->_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
            }
            break;
        case WM_LBUTTONDBLCLK:
        {
            LRESULT result = 0;
            if (editor->_originalEditProc)
            {
                result = CallWindowProcW(editor->_originalEditProc, hwnd, msg, wParam, lParam);
            }

            DWORD selStart = 0;
            DWORD selEnd = 0;
            SendMessageW(hwnd, EM_GETSEL, reinterpret_cast<WPARAM>(&selStart), reinterpret_cast<LPARAM>(&selEnd));
            if (selEnd > selStart)
            {
                int textLength = GetWindowTextLengthW(hwnd);
                if (textLength > 0 && selEnd <= static_cast<DWORD>(textLength))
                {
                    std::wstring text(static_cast<size_t>(textLength) + 1, L'\0');
                    GetWindowTextW(hwnd, text.data(), textLength + 1);
                    text.resize(static_cast<size_t>(textLength));
                    std::wstring selection = text.substr(selStart, selEnd - selStart);
                    if (IsIdentifierToken(selection) && editor->_parent)
                    {
                        auto* word = new std::wstring(selection);
                        PostMessageW(editor->_parent, kEditorOpenPluginMessage, 0, reinterpret_cast<LPARAM>(word));
                    }
                }
            }
            return result;
        }
        case WM_LBUTTONDOWN:
        case WM_LBUTTONUP:
        case WM_PASTE:
            if (msg == WM_PASTE)
            {
                invalidateGutter(false);
            }
            if (editor->_render)
            {
                RedrawWindow(editor->_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
            }
            break;
        case WM_RBUTTONUP:
        {
            DWORD selStart = 0;
            DWORD selEnd = 0;
            SendMessageW(hwnd, EM_GETSEL, reinterpret_cast<WPARAM>(&selStart), reinterpret_cast<LPARAM>(&selEnd));
            if (selEnd > selStart)
            {
                int textLength = GetWindowTextLengthW(hwnd);
                if (textLength > 0 && selEnd <= static_cast<DWORD>(textLength))
                {
                    std::wstring text(static_cast<size_t>(textLength) + 1, L'\0');
                    GetWindowTextW(hwnd, text.data(), textLength + 1);
                    text.resize(static_cast<size_t>(textLength));
                    std::wstring selection = text.substr(selStart, selEnd - selStart);
                    if (IsIdentifierToken(selection))
                    {
                        POINT pt{ GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
                        ClientToScreen(hwnd, &pt);
                        HMENU menu = CreatePopupMenu();
                        if (menu)
                        {
                            constexpr UINT kContextOpenVst = 2001;
                            AppendMenuW(menu, MF_STRING, kContextOpenVst, L"Open VST");
                            UINT cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON,
                                pt.x, pt.y, 0, hwnd, nullptr);
                            DestroyMenu(menu);
                            if (cmd == kContextOpenVst && editor && editor->_parent)
                            {
                                auto* word = new std::wstring(selection);
                                PostMessageW(editor->_parent, kEditorOpenPluginMessage, 0, reinterpret_cast<LPARAM>(word));
                                return 0;
                            }
                        }
                    }
                }
            }
            if (editor && editor->_originalEditProc)
            {
                return CallWindowProcW(editor->_originalEditProc, hwnd, msg, wParam, lParam);
            }
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
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

LRESULT CALLBACK EditorView::RenderProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    EditorView* editor = reinterpret_cast<EditorView*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (msg == WM_NCCREATE)
    {
        auto create = reinterpret_cast<CREATESTRUCTW*>(lParam);
        editor = reinterpret_cast<EditorView*>(create->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(editor));
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    if (!editor)
    {
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    switch (msg)
    {
    case WM_NCHITTEST:
        return HTTRANSPARENT;
    case WM_ERASEBKGND:
        return 1;
    case WM_PAINT:
    {
        PAINTSTRUCT ps{};
        HDC hdc = BeginPaint(hwnd, &ps);
        if (hdc)
        {
            RECT rect{};
            GetClientRect(hwnd, &rect);
            editor->RenderOverlay(hdc, rect.right - rect.left, rect.bottom - rect.top);
        }
        EndPaint(hwnd, &ps);
        return 0;
    }
    default:
        break;
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
        _gutterWidth = std::max<int>(kGutterMinWidth, static_cast<int>(size.cx + 16 + kFoldIconAreaWidth));
    }
    else
    {
        _gutterWidth = std::max<int>(kGutterMinWidth, 10 * digits + 16 + kFoldIconAreaWidth);
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

void EditorView::UpdateCharMetrics()
{
    if (!_font || !_editor)
    {
        return;
    }

    HDC hdc = GetDC(_editor);
    HFONT oldFont = static_cast<HFONT>(SelectObject(hdc, _font));
    TEXTMETRICW tm{};
    GetTextMetricsW(hdc, &tm);
    _lineHeight = tm.tmHeight + tm.tmExternalLeading;
    SIZE size{};
    GetTextExtentPoint32W(hdc, L"M", 1, &size);
    _charWidth = std::max(1, static_cast<int>(size.cx));
    SelectObject(hdc, oldFont);
    ReleaseDC(_editor, hdc);
}

void EditorView::LoadVisualConfig()
{
    const DWORD now = GetTickCount();
    if (_lastConfigCheckTick != 0 && (now - _lastConfigCheckTick) < 250)
    {
        return;
    }
    _lastConfigCheckTick = now;

    namespace fs = std::filesystem;
    if (_visualConfigPath.empty())
    {
        fs::path cwd = fs::current_path();
        fs::path candidates[] = {
            cwd / "VisualScripts" / "text_visuals.json",
            cwd / "MusicEngineEditor" / "VisualScripts" / "text_visuals.json",
            cwd.parent_path() / "MusicEngineEditor" / "VisualScripts" / "text_visuals.json"
        };
        for (const auto& path : candidates)
        {
            if (fs::exists(path))
            {
                _visualConfigPath = path.wstring();
                break;
            }
        }
    }

    if (_visualConfigPath.empty())
    {
        return;
    }

    fs::path configPath(_visualConfigPath);
    if (!fs::exists(configPath))
    {
        return;
    }

    FILETIME currentWriteTime{};
    HANDLE fileHandle = CreateFileW(configPath.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL, nullptr);
    if (fileHandle == INVALID_HANDLE_VALUE)
    {
        return;
    }
    GetFileTime(fileHandle, nullptr, nullptr, &currentWriteTime);
    CloseHandle(fileHandle);

    if (_visualConfigWriteTime.dwLowDateTime == currentWriteTime.dwLowDateTime &&
        _visualConfigWriteTime.dwHighDateTime == currentWriteTime.dwHighDateTime)
    {
        return;
    }

    _visualConfigWriteTime = currentWriteTime;

    std::ifstream file(configPath, std::ios::binary);
    if (!file)
    {
        return;
    }

    std::string contents((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
    RandomGlowConfig cfg = ParseRandomGlow(contents);
    _randomGlowEnabled = cfg.enabled;
    _randomGlowSeed = cfg.seed;
    _randomGlowIntensity = cfg.intensity;
    _randomGlowRadius = cfg.radius;
    _randomGlowSoftness = cfg.softness;
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
