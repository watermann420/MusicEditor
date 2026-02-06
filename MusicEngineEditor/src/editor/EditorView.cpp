#include "editor/EditorView.h"
#include "app/CommandIds.h"

#include <algorithm>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <string>

#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "msimg32.lib")

namespace
{
    constexpr COLORREF kBgColor = RGB(18, 18, 20);
    constexpr COLORREF kTextColor = RGB(225, 225, 225);
    constexpr COLORREF kGutterColor = RGB(24, 24, 28);
    constexpr COLORREF kGutterTextColor = RGB(140, 140, 140);
    constexpr int kEditorPadding = 16;
    constexpr int kGutterMinWidth = 36;

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
}

void EditorView::Initialize(HWND parent)
{
    _parent = parent;
    _bgBrush = CreateSolidBrush(kBgColor);
    Gdiplus::GdiplusStartupInput gdiplusStartupInput{};
    Gdiplus::GdiplusStartup(&_gdiplusToken, &gdiplusStartupInput, nullptr);
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

    RECT rect{};
    GetClientRect(_parent, &rect);
    const int width = rect.right - rect.left;
    const int height = rect.bottom - rect.top;
    const int x = kEditorPadding + _gutterWidth;
    const int y = kEditorPadding + topOffset;
    const int w = width - (kEditorPadding * 2) - _gutterWidth;
    const int h = height - (kEditorPadding * 2) - topOffset - bottomOffset;
    MoveWindow(_editor, x, y, w, h, TRUE);
    if (_render)
    {
        MoveWindow(_render, x, y, w, h, TRUE);
        SetWindowPos(_render, _editor, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
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
    LoadVisualConfig();
    SetTextColor(hdc, _randomGlowEnabled ? kBgColor : kTextColor);
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
    if (!_parent || !_editor || !_randomGlowEnabled)
    {
        return;
    }

    LoadVisualConfig();

    int firstLine = static_cast<int>(SendMessageW(_editor, EM_GETFIRSTVISIBLELINE, 0, 0));
    int totalLines = GetLineCount();
    int visibleLines = _lineHeight > 0 ? height / _lineHeight + 1 : 0;

    graphics.SetSmoothingMode(Gdiplus::SmoothingModeHighSpeed);
    graphics.SetCompositingQuality(Gdiplus::CompositingQualityHighSpeed);
    graphics.SetInterpolationMode(Gdiplus::InterpolationModeLowQuality);
    graphics.SetPixelOffsetMode(Gdiplus::PixelOffsetModeHighSpeed);

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

    bool cacheInvalid =
        _cacheGlowSeed != _randomGlowSeed ||
        _cacheGlowIntensity != _randomGlowIntensity ||
        _cacheGlowRadius != _randomGlowRadius ||
        _cacheGlowSoftness != _randomGlowSoftness ||
        _cacheColumns != visibleColumns ||
        _cacheLineHeight != _lineHeight ||
        _cacheScrollX != scrollX;

    if (cacheInvalid)
    {
        _lineCache.clear();
        _cacheGlowSeed = _randomGlowSeed;
        _cacheGlowIntensity = _randomGlowIntensity;
        _cacheGlowRadius = _randomGlowRadius;
        _cacheGlowSoftness = _randomGlowSoftness;
        _cacheColumns = visibleColumns;
        _cacheLineHeight = _lineHeight;
        _cacheScrollX = scrollX;
    }

    const int glowSpread = std::max<int>(1, static_cast<int>(std::round(_randomGlowRadius / 3.0f)));
    const float softness = std::clamp(_randomGlowSoftness, 0.1f, 1.0f);
    const BYTE glowAlphaBase = static_cast<BYTE>(std::clamp(_randomGlowIntensity, 0.0f, 1.0f) * 255.0f);

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
        bool needsRender = entry.text != line || !entry.bitmap || entry.width != lineWidth || entry.height != _lineHeight;

        if (needsRender)
        {
            entry.text = line;
            entry.width = lineWidth;
            entry.height = _lineHeight;
            entry.bitmap = std::make_unique<Gdiplus::Bitmap>(lineWidth, _lineHeight, PixelFormat32bppPARGB);

            Gdiplus::Graphics lineGraphics(entry.bitmap.get());
            lineGraphics.SetTextRenderingHint(Gdiplus::TextRenderingHintSingleBitPerPixelGridFit);
            lineGraphics.SetSmoothingMode(Gdiplus::SmoothingModeHighSpeed);
            lineGraphics.SetCompositingQuality(Gdiplus::CompositingQualityHighSpeed);
            lineGraphics.SetInterpolationMode(Gdiplus::InterpolationModeLowQuality);
            lineGraphics.SetPixelOffsetMode(Gdiplus::PixelOffsetModeHighSpeed);
            lineGraphics.Clear(Gdiplus::Color(0, 0, 0, 0));

            Gdiplus::SolidBrush glowBrush(Gdiplus::Color(0, 0, 0, 0));
            Gdiplus::SolidBrush textBrush(Gdiplus::Color(255, 255, 255, 255));
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

                unsigned int hash = HashColor(_randomGlowSeed, static_cast<unsigned int>((lineNo * 131u) + col));
                Gdiplus::Color textColor = ColorFromHash(hash, 255);
                Gdiplus::Color glowColor = ColorFromHash(hash ^ 0x5bd1e995u, glowAlphaBase);

                float x = static_cast<float>(static_cast<int>(col - startColumn) * _charWidth);
                glowBrush.SetColor(glowColor);
                textBrush.SetColor(textColor);

                int offset = static_cast<int>(std::round(glowSpread * softness));
                if (offset < 1)
                {
                    offset = 1;
                }
                const int offsets[4][2] = { { -offset, 0 }, { offset, 0 }, { 0, -offset }, { 0, offset } };
                for (const auto& off : offsets)
                {
                    Gdiplus::PointF pos(x + off[0], static_cast<float>(off[1]));
                    wchar_t buffer[2]{ ch, L'\0' };
                    lineGraphics.DrawString(buffer, 1, _gdiFont, pos, &glowBrush);
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

LRESULT CALLBACK EditorView::EditProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    EditorView* editor = reinterpret_cast<EditorView*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    HWND parent = GetParent(hwnd);

    if (editor)
    {
        auto invalidateGutter = [editor, parent]()
        {
            if (!editor->_editor || !parent)
            {
                return;
            }
            RECT editRect{};
            GetWindowRect(editor->_editor, &editRect);
            POINT topLeft{ editRect.left, editRect.top };
            POINT bottomRight{ editRect.right, editRect.bottom };
            ScreenToClient(parent, &topLeft);
            ScreenToClient(parent, &bottomRight);
            RECT gutter{ topLeft.x - editor->_gutterWidth, topLeft.y, topLeft.x, bottomRight.y };
            InvalidateRect(parent, &gutter, FALSE);
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
                return 0;
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
            invalidateGutter();
            if (editor->_render)
            {
                RedrawWindow(editor->_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
            }
            break;
        case WM_KEYUP:
            if (wParam == VK_RETURN || wParam == VK_BACK || wParam == VK_DELETE)
            {
                invalidateGutter();
            }
            if (editor->_render)
            {
                RedrawWindow(editor->_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
            }
            break;
        case WM_CHAR:
            if (wParam == L'\r' || wParam == L'\n')
            {
                invalidateGutter();
            }
            if (editor->_render)
            {
                RedrawWindow(editor->_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
            }
            break;
        case WM_LBUTTONDOWN:
        case WM_LBUTTONUP:
        case WM_PASTE:
            if (msg == WM_PASTE)
            {
                invalidateGutter();
            }
            if (editor->_render)
            {
                RedrawWindow(editor->_render, nullptr, nullptr, RDW_INVALIDATE | RDW_NOERASE);
            }
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
