#pragma once

#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <windows.h>
#include <string>

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

private:
    static LRESULT CALLBACK EditProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
    void UpdateGutterWidth();
    int GetLineCount() const;

    HWND _parent = nullptr;
    HWND _editor = nullptr;
    HFONT _font = nullptr;
    HBRUSH _bgBrush = nullptr;
    WNDPROC _originalEditProc = nullptr;

    int _lineHeight = 16;
    int _gutterWidth = 36;
};
