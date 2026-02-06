#pragma once

#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <windows.h>
#include <string>
#include <memory>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include <gdiplus.h>

class EditorView
{
public:
    void Initialize(HWND parent);
    void Resize(int topOffset, int bottomOffset);
    void Shutdown();
    LRESULT OnEditColor(HDC hdc);
    void DrawLineNumbers(HDC hdc);
    std::wstring GetText() const;
    void SetText(const std::wstring& text);
    void SetActiveNote(int note, bool active);
    bool PruneExpiredNotes();

private:
    struct LineCacheEntry
    {
        std::wstring text;
        std::unique_ptr<Gdiplus::Bitmap> bitmap;
        int width = 0;
        int height = 0;
    };

    struct NoteGlowState
    {
        bool active = false;
        DWORD lastOnTick = 0;
        DWORD lastOffTick = 0;
    };

    static LRESULT CALLBACK EditProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
    static LRESULT CALLBACK RenderProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
    void DrawCustomTextToGraphics(Gdiplus::Graphics& graphics, int width, int height);
    void RenderOverlay(HDC hdc, int width, int height);
    void UpdateGutterWidth();
    int GetLineCount() const;
    void UpdateCharMetrics();
    void LoadVisualConfig();
    void BuildSyntaxColors(const std::wstring& line, std::vector<Gdiplus::Color>& colors,
        std::vector<float>& glow, DWORD now) const;

    HWND _parent = nullptr;
    HWND _editor = nullptr;
    HWND _render = nullptr;
    HFONT _font = nullptr;
    HBRUSH _bgBrush = nullptr;
    WNDPROC _originalEditProc = nullptr;
    Gdiplus::Font* _gdiFont = nullptr;

    int _lineHeight = 16;
    int _gutterWidth = 36;
    int _charWidth = 8;
    ULONG_PTR _gdiplusToken = 0;
    bool _randomGlowEnabled = false;
    unsigned int _randomGlowSeed = 1337;
    float _randomGlowIntensity = 0.6f;
    float _randomGlowRadius = 9.0f;
    float _randomGlowSoftness = 0.7f;
    std::wstring _visualConfigPath;
    FILETIME _visualConfigWriteTime{};
    DWORD _lastConfigCheckTick = 0;
    unsigned int _cacheGlowSeed = 0;
    float _cacheGlowIntensity = 0.0f;
    float _cacheGlowRadius = 0.0f;
    float _cacheGlowSoftness = 0.0f;
    int _cacheColumns = 0;
    int _cacheLineHeight = 0;
    int _cacheScrollX = 0;
    unsigned int _cacheActiveNotesVersion = 0;
    std::unordered_map<int, LineCacheEntry> _lineCache;
    std::unordered_map<int, NoteGlowState> _noteGlow;
    unsigned int _activeNotesVersion = 0;
    bool _syntaxOverlayEnabled = true;
};
