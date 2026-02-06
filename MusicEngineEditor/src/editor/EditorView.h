#pragma once

#include <windows.h>

class EditorView
{
public:
    void Initialize(HWND parent);
    void Resize();
    void Shutdown();
    LRESULT OnEditColor(HDC hdc);
    void DrawLineNumbers();

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
