#ifndef _LOCALIZATION_MANAGER_H_
#define _LOCALIZATION_MANAGER_H_

#include <string>
#include <unordered_map>
#include <vector>

class LocalizationManager
{
public:
    static LocalizationManager& instance();

    void init(const std::string& preferred_lang = "");
    bool load_from_file(const std::string& xml_file_path);

    std::string get(const std::string& key, const std::vector<std::string>& args = {}) const;

    const std::string& current_language() const { return current_lang_; }

private:
    LocalizationManager();
    void load_default_fallback_strings();
    std::string format_string(const std::string& template_str, const std::vector<std::string>& args) const;

    std::unordered_map<std::string, std::string> strings_;
    std::string current_lang_{"en"};
};

namespace I18n {
    inline std::string get(const std::string& key, const std::vector<std::string>& args = {})
    {
        return LocalizationManager::instance().get(key, args);
    }

    inline std::string format(const std::string& key, const std::string& arg0)
    {
        return LocalizationManager::instance().get(key, { arg0 });
    }

    inline std::string format(const std::string& key, const std::string& arg0, const std::string& arg1)
    {
        return LocalizationManager::instance().get(key, { arg0, arg1 });
    }

    inline std::string format(const std::string& key, const std::string& arg0, const std::string& arg1, const std::string& arg2)
    {
        return LocalizationManager::instance().get(key, { arg0, arg1, arg2 });
    }
}

#endif // _LOCALIZATION_MANAGER_H_
