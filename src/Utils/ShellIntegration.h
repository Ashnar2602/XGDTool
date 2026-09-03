#ifndef _SHELL_INTEGRATION_H_
#define _SHELL_INTEGRATION_H_

#if defined(_WIN32)
#  ifndef NOMINMAX
#    define NOMINMAX
#  endif
#  ifndef WIN32_LEAN_AND_MEAN
#    define WIN32_LEAN_AND_MEAN
#  endif
#  include <windows.h>
#  include <filesystem>
#  include <string>
#  include <vector>

namespace ShellIntegration {

inline bool register_context_menu(const std::filesystem::path& exe_path) {
    std::wstring exe = exe_path.wstring();
    std::vector<std::wstring> extensions = { L".iso", L".xiso", L".cso", L".cci", L".zar" };

    for (const auto& ext : extensions) {
        std::wstring subkey = L"Software\\Classes\\SystemFileAssociations\\" + ext + L"\\shell\\XGDTool";
        HKEY hKey;
        if (RegCreateKeyExW(HKEY_CURRENT_USER, subkey.c_str(), 0, NULL, 0, KEY_WRITE, NULL, &hKey, NULL) == ERROR_SUCCESS) {
            std::wstring menu_text = L"Open with XGDTool";
            RegSetValueExW(hKey, NULL, 0, REG_SZ, reinterpret_cast<const BYTE*>(menu_text.c_str()), static_cast<DWORD>((menu_text.size() + 1) * sizeof(wchar_t)));
            RegSetValueExW(hKey, L"Icon", 0, REG_SZ, reinterpret_cast<const BYTE*>(exe.c_str()), static_cast<DWORD>((exe.size() + 1) * sizeof(wchar_t)));

            HKEY hCmdKey;
            if (RegCreateKeyExW(hKey, L"command", 0, NULL, 0, KEY_WRITE, NULL, &hCmdKey, NULL) == ERROR_SUCCESS) {
                std::wstring cmd = L"\"" + exe + L"\" \"%1\"";
                RegSetValueExW(hCmdKey, NULL, 0, REG_SZ, reinterpret_cast<const BYTE*>(cmd.c_str()), static_cast<DWORD>((cmd.size() + 1) * sizeof(wchar_t)));
                RegCloseKey(hCmdKey);
            }
            RegCloseKey(hKey);
        }
    }
    return true;
}

inline bool unregister_context_menu() {
    std::vector<std::wstring> extensions = { L".iso", L".xiso", L".cso", L".cci", L".zar" };
    for (const auto& ext : extensions) {
        std::wstring cmd_key = L"Software\\Classes\\SystemFileAssociations\\" + ext + L"\\shell\\XGDTool\\command";
        std::wstring shell_key = L"Software\\Classes\\SystemFileAssociations\\" + ext + L"\\shell\\XGDTool";
        RegDeleteKeyW(HKEY_CURRENT_USER, cmd_key.c_str());
        RegDeleteKeyW(HKEY_CURRENT_USER, shell_key.c_str());
    }
    return true;
}

} // namespace ShellIntegration

#endif // _WIN32

#endif // _SHELL_INTEGRATION_H_
