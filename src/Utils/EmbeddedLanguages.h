#pragma once

#include <string_view>

namespace EmbeddedLanguages {

inline constexpr std::string_view XML_EN = R"xml(<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="app_name">XGDTool</string>
    <string name="notification_title">XGDTool - Processing Complete</string>
    <string name="batch_completed_all">Conversion complete: {0} of {1} succeeded</string>
    <string name="batch_completed_with_errors">Conversion complete: {0} of {1} succeeded, {2} failed</string>
    
    <string name="dialog_title_success">Processing Complete</string>
    <string name="dialog_title_warning">Processing Completed with Errors</string>
    <string name="dialog_msg_all_ok">All {0} files have been successfully processed!</string>
    <string name="dialog_msg_single_ok">File successfully processed!</string>
    <string name="dialog_msg_errors">Processing finished with errors:\n\n• Succeeded: {0}\n• Failed: {1}\n\nCheck the log file for detailed diagnostics.</string>
    <string name="dialog_msg_cancelled">Processing cancelled by user.\n\n• Succeeded: {0}\n• Incomplete/Failed: {1}</string>
    
    <string name="btn_open_log">Open Log File</string>
    <string name="btn_close">Close</string>
    <string name="btn_ok">OK</string>

    <string name="label_input_path">Input Path:</string>
    <string name="label_output_dir">Output Directory:</string>
    <string name="label_file_list">File List:</string>
    <string name="btn_browse">Browse</string>
    <string name="col_format">Format</string>
    <string name="col_filename">Filename</string>
    <string name="label_status">Status:</string>
    <string name="label_current_progress">Current Progress:</string>
    <string name="label_total_progress">Total Progress:</string>

    <string name="section_output_format">Output Format:</string>
    <string name="section_scrub">Scrub:</string>
    <string name="section_settings">Settings:</string>
    <string name="section_language">Language:</string>

    <string name="scrub_none">None</string>
    <string name="scrub_partial">Partial</string>
    <string name="scrub_full">Full</string>

    <string name="setting_split">Split XISO</string>
    <string name="setting_attach_xbe">Generate Attach XBE</string>
    <string name="setting_am_patch">Allowed Media XBE Patch</string>
    <string name="setting_rename_xbe">Rename XBE Title</string>
    <string name="setting_offline_mode">Offline Mode</string>
    <string name="setting_keep_name">Keep Original Name</string>

    <string name="lang_system">System</string>
    <string name="lang_english">English</string>
    <string name="lang_italian">Italiano</string>
    <string name="lang_german">Deutsch</string>
    <string name="lang_french">Français</string>
    <string name="lang_spanish">Español</string>
    <string name="lang_portuguese">Português</string>

    <string name="btn_process_all">Process All</string>
    <string name="btn_pause">Pause</string>
    <string name="btn_resume">Resume</string>
    <string name="btn_cancel">Cancel</string>

    <string name="status_idle">Idle</string>
    <string name="status_paused">Paused</string>
    <string name="status_processing">Processing input files</string>
    <string name="status_complete">Processing complete</string>
    <string name="status_cancelled">Processing cancelled</string>

    <string name="choose_selection_type_title">Choose the type of selection:</string>
    <string name="choose_selection_type_caption">Select</string>
    <string name="choice_select_files">Select File(s)</string>
    <string name="choice_select_dir">Select Directory</string>
    <string name="dialog_select_files_title">Select file(s)</string>
    <string name="dialog_select_dir_title">Select a directory</string>
    <string name="dialog_select_out_dir_title">Select a GoD/Game/Batch directory</string>
    <string name="wildcard_xbox_images">Xbox image files (*.iso;*.cci;*.cso;*.zar)|*.iso;*.cci;*.cso;*.zar|All files (*.*)|*.*</string>
    <string name="msg_no_input_files">No input files selected</string>
    <string name="msg_no_output_dir">No output directory selected</string>
    <string name="msg_no_valid_files">No valid files found in selected input path</string>

    <string name="tooltip_browse_input">Select the input file or directory to process</string>
    <string name="tooltip_browse_output">Select the output directory to save the processed files</string>
    <string name="tooltip_fmt_iso">Creates an XISO image</string>
    <string name="tooltip_fmt_god">Creates a Games on Demand image</string>
    <string name="tooltip_fmt_cci">Creates a CCI archive</string>
    <string name="tooltip_fmt_cso">Creates a CSO archive</string>
    <string name="tooltip_fmt_zar">Creates a ZAR archive</string>
    <string name="tooltip_fmt_extract">Extracts all files to a directory</string>
    <string name="tooltip_auto_ogxbox">Automatically choose format and settings for use with OG Xbox</string>
    <string name="tooltip_auto_xbox360">Automatically choose format and settings for use with Xbox 360</string>
    <string name="tooltip_auto_xemu">Automatically choose format and settings for use with Xemu</string>
    <string name="tooltip_auto_xenia">Automatically choose format and settings for use with Xenia</string>
    <string name="tooltip_scrub_none">No scrubbing, only video partion is removed if present</string>
    <string name="tooltip_scrub_partial">Scrubs and trims the output image, random padding data is removed</string>
    <string name="tooltip_scrub_full">Completely reauthor the resulting image, this will produce the smallest file possible</string>
    <string name="tooltip_split">Splits the resulting XISO file if it's too large for OG Xbox</string>
    <string name="tooltip_attach_xbe">Generates an attach XBE file along with the output file</string>
    <string name="tooltip_am_patch">Patches the Allowed Media field in resulting XBE files</string>
    <string name="tooltip_rename_xbe">Replaces the title field of resulting XBE files with one found in the database</string>
    <string name="tooltip_offline_mode">Disables online functionality, will result in less accurate file naming</string>
    <string name="tooltip_keep_name">Keeps the original input filename for output files, preventing overwrites for multi-disc games</string>
    <string name="tooltip_lang_system">Use system default language</string>
    <string name="tooltip_lang_english">Set UI language to English</string>
    <string name="tooltip_lang_italian">Set UI language to Italian</string>
    <string name="tooltip_lang_german">Set UI language to German</string>
    <string name="tooltip_lang_french">Set UI language to French</string>
    <string name="tooltip_lang_spanish">Set UI language to Spanish</string>
    <string name="tooltip_lang_portuguese">Set UI language to Portuguese</string>
    <string name="tooltip_process_all">Process all files in the File List</string>
    <string name="tooltip_pause">Pause processing of files</string>
    <string name="tooltip_cancel">Processing will stop after the current file is finished</string>

    <string name="cli_opt_input_path">Input path</string>
    <string name="cli_opt_output_dir">Output directory</string>
    <string name="cli_group_output_format">Output Format</string>
    <string name="cli_group_settings">Settings</string>
    <string name="cli_flag_extract">Extracts all files to a directory</string>
    <string name="cli_flag_xiso">Creates an XISO image</string>
    <string name="cli_flag_god">Creates a Games on Demand image</string>
    <string name="cli_flag_cci">Creates a CCI archive</string>
    <string name="cli_flag_cso">Creates a CSO archive</string>
    <string name="cli_flag_zar">Creates a ZAR archive</string>
    <string name="cli_flag_xbe">Generates an attach XBE file</string>
    <string name="cli_flag_ogxbox">Automatically choose format and settings for use with OG Xbox</string>
    <string name="cli_flag_xbox360">Automatically choose format and settings for use with Xbox 360</string>
    <string name="cli_flag_xemu">Automatically choose format and settings for use with Xemu</string>
    <string name="cli_flag_xenia">Automatically choose format and settings for use with Xenia</string>
    <string name="cli_flag_list">List file contents of input image</string>
    <string name="cli_flag_help">Print this help message and exit</string>
    <string name="cli_flag_partial_scrub">Scrubs and trims the output image, random padding data is removed</string>
    <string name="cli_flag_full_scrub">Completely reauthor the resulting image, this will produce the smallest file possible</string>
    <string name="cli_flag_split">Splits the resulting XISO file if it's too large for OG Xbox</string>
    <string name="cli_flag_rename">Patches the title field of resulting XBE files to one found in the database</string>
    <string name="cli_flag_attach_xbe">Generates an attach XBE file along with the output file</string>
    <string name="cli_flag_am_patch">Patches the Allowed Media field in resulting XBE files</string>
    <string name="cli_flag_offline">Disables online functionality, will result in less accurate file naming</string>
    <string name="cli_flag_keep_name">Keep original input filename for output instead of database title lookup</string>
    <string name="cli_flag_lang">Set interface language (e.g. 'it', 'en', 'system')</string>
    <string name="cli_flag_debug">Enable debug logging</string>
    <string name="cli_flag_quiet">Disable all logging except for warnings and errors</string>

    <string name="cli_msg_input_not_exist">Input path does not exist: {0}</string>
    <string name="cli_msg_failed_input">Failed to process input: {0}</string>
    <string name="cli_msg_finished">Finished processing input files.</string>
    <string name="cli_msg_processing">Processing: {0}</string>
    <string name="cli_msg_success_created">Successfully created: {0}</string>
    <string name="cli_msg_files_in_image">Files in image:</string>

    <string name="stage_writing_zar">Writing files to ZAR archive</string>
    <string name="stage_writing_xiso">Writing XISO</string>
    <string name="stage_writing_god">Writing GoD data files</string>
    <string name="stage_writing_cso">Writing CSO file</string>
    <string name="stage_writing_cci">Writing CCI archive</string>
    <string name="stage_extracting">Extracting files</string>
</resources>
)xml";

inline constexpr std::string_view XML_IT = R"xml(<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="app_name">XGDTool</string>
    <string name="notification_title">XGDTool - Elaborazione Terminata</string>
    <string name="batch_completed_all">Conversione completata: {0} di {1} riusciti</string>
    <string name="batch_completed_with_errors">Conversione completata: {0} di {1} riusciti, {2} falliti</string>
    
    <string name="dialog_title_success">Elaborazione Completata</string>
    <string name="dialog_title_warning">Elaborazione Completata con Errori</string>
    <string name="dialog_msg_all_ok">Tutti i {0} file sono stati elaborati con successo!</string>
    <string name="dialog_msg_single_ok">File elaborato con successo!</string>
    <string name="dialog_msg_errors">Elaborazione terminata con errori:\n\n• File riusciti: {0}\n• File falliti: {1}\n\nConsulta il file di log per maggiori dettagli.</string>
    <string name="dialog_msg_cancelled">Elaborazione annullata dall'utente.\n\n• File completati: {0}\n• File interrotti/falliti: {1}</string>
    
    <string name="btn_open_log">Apri File di Log</string>
    <string name="btn_close">Chiudi</string>
    <string name="btn_ok">OK</string>

    <string name="label_input_path">Percorso Input:</string>
    <string name="label_output_dir">Cartella di Output:</string>
    <string name="label_file_list">Lista File:</string>
    <string name="btn_browse">Sfoglia</string>
    <string name="col_format">Formato</string>
    <string name="col_filename">Nome file</string>
    <string name="label_status">Stato:</string>
    <string name="label_current_progress">Avanzamento Attuale:</string>
    <string name="label_total_progress">Avanzamento Totale:</string>

    <string name="section_output_format">Formato Output:</string>
    <string name="section_scrub">Scrub:</string>
    <string name="section_settings">Impostazioni:</string>
    <string name="section_language">Lingua:</string>

    <string name="scrub_none">Nessuno</string>
    <string name="scrub_partial">Parziale</string>
    <string name="scrub_full">Completo</string>

    <string name="setting_split">Dividi XISO</string>
    <string name="setting_attach_xbe">Genera Attach XBE</string>
    <string name="setting_am_patch">Patch Allowed Media XBE</string>
    <string name="setting_rename_xbe">Rinomina Titolo XBE</string>
    <string name="setting_offline_mode">Modalità Offline</string>
    <string name="setting_keep_name">Mantieni nome originale</string>

    <string name="lang_system">Sistema</string>
    <string name="lang_english">English</string>
    <string name="lang_italian">Italiano</string>
    <string name="lang_german">Deutsch</string>
    <string name="lang_french">Français</string>
    <string name="lang_spanish">Español</string>
    <string name="lang_portuguese">Português</string>

    <string name="btn_process_all">Elabora Tutto</string>
    <string name="btn_pause">Pausa</string>
    <string name="btn_resume">Riprendi</string>
    <string name="btn_cancel">Annulla</string>

    <string name="status_idle">In attesa</string>
    <string name="status_paused">In pausa</string>
    <string name="status_processing">Elaborazione file in corso</string>
    <string name="status_complete">Elaborazione completata</string>
    <string name="status_cancelled">Elaborazione annullata</string>

    <string name="choose_selection_type_title">Scegli il tipo di selezione:</string>
    <string name="choose_selection_type_caption">Seleziona</string>
    <string name="choice_select_files">Seleziona File</string>
    <string name="choice_select_dir">Seleziona Cartella</string>
    <string name="dialog_select_files_title">Seleziona uno o più file</string>
    <string name="dialog_select_dir_title">Seleziona una cartella</string>
    <string name="dialog_select_out_dir_title">Seleziona cartella di destinazione (GoD/Gioco/Batch)</string>
    <string name="wildcard_xbox_images">File immagine Xbox (*.iso;*.cci;*.cso;*.zar)|*.iso;*.cci;*.cso;*.zar|Tutti i file (*.*)|*.*</string>
    <string name="msg_no_input_files">Nessun file di input selezionato</string>
    <string name="msg_no_output_dir">Nessuna cartella di output selezionata</string>
    <string name="msg_no_valid_files">Nessun file valido trovato nel percorso selezionato</string>

    <string name="tooltip_browse_input">Seleziona il file o la cartella da elaborare</string>
    <string name="tooltip_browse_output">Seleziona la cartella in cui salvare i file elaborati</string>
    <string name="tooltip_fmt_iso">Crea un'immagine XISO</string>
    <string name="tooltip_fmt_god">Crea un'immagine Games on Demand (GoD)</string>
    <string name="tooltip_fmt_cci">Crea un archivio compresso CCI</string>
    <string name="tooltip_fmt_cso">Crea un archivio compresso CSO</string>
    <string name="tooltip_fmt_zar">Crea un archivio compresso ZAR</string>
    <string name="tooltip_fmt_extract">Estrae tutti i file in una cartella</string>
    <string name="tooltip_auto_ogxbox">Sceglie automaticamente formato e impostazioni per Xbox originale</string>
    <string name="tooltip_auto_xbox360">Sceglie automaticamente formato e impostazioni per Xbox 360</string>
    <string name="tooltip_auto_xemu">Sceglie automaticamente formato e impostazioni per Xemu</string>
    <string name="tooltip_auto_xenia">Sceglie automaticamente formato e impostazioni per Xenia</string>
    <string name="tooltip_scrub_none">Nessuno scrub, viene rimossa solo la partizione video se presente</string>
    <string name="tooltip_scrub_partial">Esegue lo scrub e il trim dell'immagine, rimuovendo dati di padding casuali</string>
    <string name="tooltip_scrub_full">Riautora completamente l'immagine per ottenere la dimensione minore possibile</string>
    <string name="tooltip_split">Divide il file XISO se supera la dimensione massima per OG Xbox</string>
    <string name="tooltip_attach_xbe">Genera un file attach XBE insieme al file di output</string>
    <string name="tooltip_am_patch">Applica la patch al campo Allowed Media nei file XBE risultanti</string>
    <string name="tooltip_rename_xbe">Sostituisce il campo titolo dei file XBE con quello trovato nel database</string>
    <string name="tooltip_offline_mode">Disabilita le funzioni online, risulterà in nomi file meno accurati</string>
    <string name="tooltip_keep_name">Mantiene il nome del file di input originale per i file di output, evitando sovrascritture per giochi multidisco</string>
    <string name="tooltip_lang_system">Usa la lingua predefinita del sistema</string>
    <string name="tooltip_lang_english">Imposta la lingua su Inglese</string>
    <string name="tooltip_lang_italian">Imposta la lingua su Italiano</string>
    <string name="tooltip_lang_german">Imposta la lingua su Tedesco</string>
    <string name="tooltip_lang_french">Imposta la lingua su Francese</string>
    <string name="tooltip_lang_spanish">Imposta la lingua su Spagnolo</string>
    <string name="tooltip_lang_portuguese">Imposta la lingua su Portoghese</string>
    <string name="tooltip_process_all">Elabora tutti i file nella lista</string>
    <string name="tooltip_pause">Mette in pausa l'elaborazione dei file</string>
    <string name="tooltip_cancel">L'elaborazione si fermerà al termine del file attuale</string>

    <string name="cli_opt_input_path">Percorso di input</string>
    <string name="cli_opt_output_dir">Cartella di output</string>
    <string name="cli_group_output_format">Formato Output</string>
    <string name="cli_group_settings">Impostazioni</string>
    <string name="cli_flag_extract">Estrae tutti i file in una cartella</string>
    <string name="cli_flag_xiso">Crea un'immagine XISO</string>
    <string name="cli_flag_god">Crea un'immagine Games on Demand (GoD)</string>
    <string name="cli_flag_cci">Crea un archivio compresso CCI</string>
    <string name="cli_flag_cso">Crea un archivio compresso CSO</string>
    <string name="cli_flag_zar">Crea un archivio compresso ZAR</string>
    <string name="cli_flag_xbe">Genera un file attach XBE</string>
    <string name="cli_flag_ogxbox">Sceglie automaticamente formato e impostazioni per OG Xbox</string>
    <string name="cli_flag_xbox360">Sceglie automaticamente formato e impostazioni per Xbox 360</string>
    <string name="cli_flag_xemu">Sceglie automaticamente formato e impostazioni per Xemu</string>
    <string name="cli_flag_xenia">Sceglie automaticamente formato e impostazioni per Xenia</string>
    <string name="cli_flag_list">Elenca il contenuto dei file nell'immagine di input</string>
    <string name="cli_flag_help">Mostra questo messaggio di aiuto ed esce</string>
    <string name="cli_flag_partial_scrub">Esegue scrub e trim dell'immagine, rimuovendo dati di padding casuali</string>
    <string name="cli_flag_full_scrub">Riautora completamente l'immagine per ottenere la dimensione minore possibile</string>
    <string name="cli_flag_split">Divide il file XISO risultante se troppo grande per OG Xbox</string>
    <string name="cli_flag_rename">Applica la patch al titolo del file XBE con quello trovato nel database</string>
    <string name="cli_flag_attach_xbe">Genera un file attach XBE insieme al file di output</string>
    <string name="cli_flag_am_patch">Applica la patch al campo Allowed Media nei file XBE risultanti</string>
    <string name="cli_flag_offline">Disabilita le funzionalità online, producendo nomi meno accurati</string>
    <string name="cli_flag_keep_name">Mantiene il nome del file di input originale per l'output invece di usare il titolo del database</string>
    <string name="cli_flag_lang">Specifica la lingua dell'interfaccia (es. 'it', 'en', 'system')</string>
    <string name="cli_flag_debug">Abilita il log di debug</string>
    <string name="cli_flag_quiet">Disabilita tutti i log eccetto avvisi ed errori</string>

    <string name="cli_msg_input_not_exist">Il percorso di input non esiste: {0}</string>
    <string name="cli_msg_failed_input">Elaborazione fallita per l'input: {0}</string>
    <string name="cli_msg_finished">Elaborazione dei file di input completata.</string>
    <string name="cli_msg_processing">Elaborazione in corso: {0}</string>
    <string name="cli_msg_success_created">Creato con successo: {0}</string>
    <string name="cli_msg_files_in_image">File presenti nell'immagine:</string>

    <string name="stage_writing_zar">Scrittura archivio ZAR</string>
    <string name="stage_writing_xiso">Scrittura XISO</string>
    <string name="stage_writing_god">Scrittura dati GoD</string>
    <string name="stage_writing_cso">Scrittura file CSO</string>
    <string name="stage_writing_cci">Scrittura archivio CCI</string>
    <string name="stage_extracting">Estrazione file</string>
</resources>
)xml";

inline constexpr std::string_view XML_DE = R"xml(<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="app_name">XGDTool</string>
    <string name="notification_title">XGDTool - Verarbeitung Abgeschlossen</string>
    <string name="batch_completed_all">Konvertierung abgeschlossen: {0} von {1} erfolgreich</string>
    <string name="batch_completed_with_errors">Konvertierung abgeschlossen: {0} von {1} erfolgreich, {2} fehlgeschlagen</string>
    
    <string name="dialog_title_success">Verarbeitung Abgeschlossen</string>
    <string name="dialog_title_warning">Verarbeitung mit Fehlern Abgeschlossen</string>
    <string name="dialog_msg_all_ok">Alle {0} Dateien wurden erfolgreich verarbeitet!</string>
    <string name="dialog_msg_single_ok">Datei erfolgreich verarbeitet!</string>
    <string name="dialog_msg_errors">Verarbeitung mit Fehlern beendet:\n\n• Erfolgreich: {0}\n• Fehlgeschlagen: {1}\n\nPrüfen Sie die Protokolldatei für Details.</string>
    <string name="dialog_msg_cancelled">Verarbeitung vom Benutzer abgebrochen.\n\n• Abgeschlossen: {0}\n• Abgebrochen/Fehlgeschlagen: {1}</string>
    
    <string name="btn_open_log">Protokolldatei öffnen</string>
    <string name="btn_close">Schließen</string>
    <string name="btn_ok">OK</string>

    <string name="label_input_path">Eingabepfad:</string>
    <string name="label_output_dir">Ausgabeverzeichnis:</string>
    <string name="label_file_list">Dateiliste:</string>
    <string name="btn_browse">Durchsuchen</string>
    <string name="col_format">Format</string>
    <string name="col_filename">Dateiname</string>
    <string name="label_status">Status:</string>
    <string name="label_current_progress">Aktueller Fortschritt:</string>
    <string name="label_total_progress">Gesamtfortschritt:</string>

    <string name="section_output_format">Ausgabeformat:</string>
    <string name="section_scrub">Bereinigung (Scrub):</string>
    <string name="section_settings">Einstellungen:</string>
    <string name="section_language">Sprache:</string>

    <string name="scrub_none">Keine</string>
    <string name="scrub_partial">Partiell</string>
    <string name="scrub_full">Vollständig</string>

    <string name="setting_split">XISO aufteilen</string>
    <string name="setting_attach_xbe">Attach-XBE erzeugen</string>
    <string name="setting_am_patch">XBE Allowed Media patchen</string>
    <string name="setting_rename_xbe">XBE-Titel umbenennen</string>
    <string name="setting_offline_mode">Offline-Modus</string>
    <string name="setting_keep_name">Originalnamen beibehalten</string>

    <string name="lang_system">System</string>
    <string name="lang_english">English</string>
    <string name="lang_italian">Italiano</string>
    <string name="lang_german">Deutsch</string>
    <string name="lang_french">Français</string>
    <string name="lang_spanish">Español</string>
    <string name="lang_portuguese">Português</string>

    <string name="btn_process_all">Alle verarbeiten</string>
    <string name="btn_pause">Pause</string>
    <string name="btn_resume">Fortsetzen</string>
    <string name="btn_cancel">Abbrechen</string>

    <string name="status_idle">Bereit</string>
    <string name="status_paused">Pausiert</string>
    <string name="status_processing">Dateien werden verarbeitet</string>
    <string name="status_complete">Verarbeitung abgeschlossen</string>
    <string name="status_cancelled">Verarbeitung abgebrochen</string>

    <string name="choose_selection_type_title">Auswahltyp wählen:</string>
    <string name="choose_selection_type_caption">Auswählen</string>
    <string name="choice_select_files">Dateien auswählen</string>
    <string name="choice_select_dir">Verzeichnis auswählen</string>
    <string name="dialog_select_files_title">Datei(en) auswählen</string>
    <string name="dialog_select_dir_title">Verzeichnis auswählen</string>
    <string name="dialog_select_out_dir_title">Ausgabeverzeichnis auswählen (GoD/Spiel/Batch)</string>
    <string name="wildcard_xbox_images">Xbox-Image-Dateien (*.iso;*.cci;*.cso;*.zar)|*.iso;*.cci;*.cso;*.zar|Alle Dateien (*.*)|*.*</string>
    <string name="msg_no_input_files">Keine Eingabedateien ausgewählt</string>
    <string name="msg_no_output_dir">Kein Ausgabeverzeichnis ausgewählt</string>
    <string name="msg_no_valid_files">Keine gültigen Dateien im gewählten Pfad gefunden</string>

    <string name="tooltip_browse_input">Eingabedatei oder -verzeichnis zur Verarbeitung auswählen</string>
    <string name="tooltip_browse_output">Ausgabeverzeichnis zum Speichern der Dateien auswählen</string>
    <string name="tooltip_fmt_iso">Erstellt ein XISO-Image</string>
    <string name="tooltip_fmt_god">Erstellt ein Games on Demand (GoD) Image</string>
    <string name="tooltip_fmt_cci">Erstellt ein komprimiertes CCI-Archiv</string>
    <string name="tooltip_fmt_cso">Erstellt ein komprimiertes CSO-Archiv</string>
    <string name="tooltip_fmt_zar">Erstellt ein komprimiertes ZAR-Archiv</string>
    <string name="tooltip_fmt_extract">Extrahiert alle Dateien in ein Verzeichnis</string>
    <string name="tooltip_auto_ogxbox">Wählt automatisch Format und Einstellungen für die Original Xbox</string>
    <string name="tooltip_auto_xbox360">Wählt automatisch Format und Einstellungen für die Xbox 360</string>
    <string name="tooltip_auto_xemu">Wählt automatisch Format und Einstellungen für Xemu</string>
    <string name="tooltip_auto_xenia">Wählt automatisch Format und Einstellungen für Xenia</string>
    <string name="tooltip_scrub_none">Keine Bereinigung, nur Videopartition wird entfernt falls vorhanden</string>
    <string name="tooltip_scrub_partial">Bereinigt und kürzt das Image, zufällige Fülldaten werden entfernt</string>
    <string name="tooltip_scrub_full">Baut das Image komplett neu auf, um die kleinstmögliche Dateigröße zu erzielen</string>
    <string name="tooltip_split">Teilt die XISO-Datei, falls sie zu groß für die Original Xbox ist</string>
    <string name="tooltip_attach_xbe">Erzeugt eine Attach-XBE-Datei zusammen mit der Ausgabedatei</string>
    <string name="tooltip_am_patch">Patcht das Feld Allowed Media in den resultierenden XBE-Dateien</string>
    <string name="tooltip_rename_xbe">Ersetzt den Titelfeld-Eintrag der XBE-Dateien mit dem Namen aus der Datenbank</string>
    <string name="tooltip_offline_mode">Deaktiviert Online-Funktionen, führt zu weniger genauen Dateinamen</string>
    <string name="tooltip_keep_name">Behält den ursprünglichen Eingabedateinamen bei (verhindert Überschreiben bei Multi-Disc-Spielen)</string>
    <string name="tooltip_lang_system">Standardsprache des Systems verwenden</string>
    <string name="tooltip_lang_english">Sprache auf Englisch setzen</string>
    <string name="tooltip_lang_italian">Sprache auf Italienisch setzen</string>
    <string name="tooltip_lang_german">Sprache auf Deutsch setzen</string>
    <string name="tooltip_lang_french">Sprache auf Französisch setzen</string>
    <string name="tooltip_lang_spanish">Sprache auf Spanisch setzen</string>
    <string name="tooltip_lang_portuguese">Sprache auf Portugiesisch setzen</string>
    <string name="tooltip_process_all">Alle Dateien in der Liste verarbeiten</string>
    <string name="tooltip_pause">Verarbeitung der Dateien pausieren</string>
    <string name="tooltip_cancel">Verarbeitung stoppt nach Fertigstellung der aktuellen Datei</string>

    <string name="cli_opt_input_path">Eingabepfad</string>
    <string name="cli_opt_output_dir">Ausgabeverzeichnis</string>
    <string name="cli_group_output_format">Ausgabeformat</string>
    <string name="cli_group_settings">Einstellungen</string>
    <string name="cli_flag_extract">Extrahiert alle Dateien in ein Verzeichnis</string>
    <string name="cli_flag_xiso">Erstellt ein XISO-Image</string>
    <string name="cli_flag_god">Erstellt ein Games on Demand (GoD) Image</string>
    <string name="cli_flag_cci">Erstellt ein CCI-Archiv</string>
    <string name="cli_flag_cso">Erstellt ein CSO-Archiv</string>
    <string name="cli_flag_zar">Erstellt ein ZAR-Archiv</string>
    <string name="cli_flag_xbe">Erzeugt eine Attach-XBE-Datei</string>
    <string name="cli_flag_ogxbox">Wählt automatisch Format und Einstellungen für OG Xbox</string>
    <string name="cli_flag_xbox360">Wählt automatisch Format und Einstellungen für Xbox 360</string>
    <string name="cli_flag_xemu">Wählt automatisch Format und Einstellungen für Xemu</string>
    <string name="cli_flag_xenia">Wählt automatisch Format und Einstellungen für Xenia</string>
    <string name="cli_flag_list">Listet Dateiinhalte des Eingabe-Images auf</string>
    <string name="cli_flag_help">Druckt diese Hilfemeldung und beendet das Programm</string>
    <string name="cli_flag_partial_scrub">Bereinigt und kürzt das Image, Padding-Daten werden entfernt</string>
    <string name="cli_flag_full_scrub">Baut das Image komplett neu auf für minimale Größe</string>
    <string name="cli_flag_split">Teilt die XISO-Datei, falls zu groß für OG Xbox</string>
    <string name="cli_flag_rename">Patcht das Titelfeld der XBE mit dem Datenbanknamen</string>
    <string name="cli_flag_attach_xbe">Erzeugt eine Attach-XBE-Datei neben der Ausgabedatei</string>
    <string name="cli_flag_am_patch">Patcht das Allowed-Media-Feld in XBE-Dateien</string>
    <string name="cli_flag_offline">Deaktiviert Online-Funktionen</string>
    <string name="cli_flag_keep_name">Behält den ursprünglichen Eingabedateinamen bei</string>
    <string name="cli_flag_lang">Sprache der Benutzeroberfläche festlegen (z.B. 'de', 'en', 'it', 'fr', 'es', 'pt', 'system')</string>
    <string name="cli_flag_debug">Debug-Protokollierung aktivieren</string>
    <string name="cli_flag_quiet">Alle Protokolle außer Warnungen und Fehlern deaktivieren</string>

    <string name="cli_msg_input_not_exist">Eingabepfad existiert nicht: {0}</string>
    <string name="cli_msg_failed_input">Eingabe konnte nicht verarbeitet werden: {0}</string>
    <string name="cli_msg_finished">Verarbeitung der Eingabedateien abgeschlossen.</string>
    <string name="cli_msg_processing">Verarbeitung: {0}</string>
    <string name="cli_msg_success_created">Erfolgreich erstellt: {0}</string>
    <string name="cli_msg_files_in_image">Dateien im Image:</string>

    <string name="stage_writing_zar">ZAR-Archiv wird geschrieben</string>
    <string name="stage_writing_xiso">XISO wird geschrieben</string>
    <string name="stage_writing_god">GoD-Daten werden geschrieben</string>
    <string name="stage_writing_cso">CSO-Datei wird geschrieben</string>
    <string name="stage_writing_cci">CCI-Archiv wird geschrieben</string>
    <string name="stage_extracting">Dateien werden extrahiert</string>
</resources>
)xml";

inline constexpr std::string_view XML_FR = R"xml(<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="app_name">XGDTool</string>
    <string name="notification_title">XGDTool - Traitement Terminé</string>
    <string name="batch_completed_all">Conversion terminée : {0} sur {1} réussis</string>
    <string name="batch_completed_with_errors">Conversion terminée : {0} sur {1} réussis, {2} échoués</string>
    
    <string name="dialog_title_success">Traitement Terminé</string>
    <string name="dialog_title_warning">Traitement Terminé avec des Erreurs</string>
    <string name="dialog_msg_all_ok">Tous les {0} fichiers ont été traités avec succès !</string>
    <string name="dialog_msg_single_ok">Fichier traité avec succès !</string>
    <string name="dialog_msg_errors">Traitement terminé avec des erreurs :\n\n• Réussis : {0}\n• Échoués : {1}\n\nConsultez le fichier journal pour plus de détails.</string>
    <string name="dialog_msg_cancelled">Traitement annulé par l'utilisateur.\n\n• Terminés : {0}\n• Interrompus/Échoués : {1}</string>
    
    <string name="btn_open_log">Ouvrir le Journal</string>
    <string name="btn_close">Fermer</string>
    <string name="btn_ok">OK</string>

    <string name="label_input_path">Chemin d'entrée :</string>
    <string name="label_output_dir">Dossier de sortie :</string>
    <string name="label_file_list">Liste des fichiers :</string>
    <string name="btn_browse">Parcourir</string>
    <string name="col_format">Format</string>
    <string name="col_filename">Nom de fichier</string>
    <string name="label_status">État :</string>
    <string name="label_current_progress">Progression actuelle :</string>
    <string name="label_total_progress">Progression totale :</string>

    <string name="section_output_format">Format de sortie :</string>
    <string name="section_scrub">Nettoyage (Scrub) :</string>
    <string name="section_settings">Paramètres :</string>
    <string name="section_language">Langue :</string>

    <string name="scrub_none">Aucun</string>
    <string name="scrub_partial">Partiel</string>
    <string name="scrub_full">Complet</string>

    <string name="setting_split">Diviser le XISO</string>
    <string name="setting_attach_xbe">Générer Attach XBE</string>
    <string name="setting_am_patch">Patcher Allowed Media XBE</string>
    <string name="setting_rename_xbe">Renommer le titre XBE</string>
    <string name="setting_offline_mode">Mode hors ligne</string>
    <string name="setting_keep_name">Conserver le nom d'origine</string>

    <string name="lang_system">Système</string>
    <string name="lang_english">English</string>
    <string name="lang_italian">Italiano</string>
    <string name="lang_german">Deutsch</string>
    <string name="lang_french">Français</string>
    <string name="lang_spanish">Español</string>
    <string name="lang_portuguese">Português</string>

    <string name="btn_process_all">Tout traiter</string>
    <string name="btn_pause">Pause</string>
    <string name="btn_resume">Reprendre</string>
    <string name="btn_cancel">Annuler</string>

    <string name="status_idle">En attente</string>
    <string name="status_paused">En pause</string>
    <string name="status_processing">Traitement des fichiers en cours</string>
    <string name="status_complete">Traitement terminé</string>
    <string name="status_cancelled">Traitement annulé</string>

    <string name="choose_selection_type_title">Choisissez le type de sélection :</string>
    <string name="choose_selection_type_caption">Sélectionner</string>
    <string name="choice_select_files">Sélectionner des fichiers</string>
    <string name="choice_select_dir">Sélectionner un dossier</string>
    <string name="dialog_select_files_title">Sélectionner un ou plusieurs fichiers</string>
    <string name="dialog_select_dir_title">Sélectionner un dossier</string>
    <string name="dialog_select_out_dir_title">Sélectionner le dossier de destination (GoD/Jeu/Batch)</string>
    <string name="wildcard_xbox_images">Fichiers images Xbox (*.iso;*.cci;*.cso;*.zar)|*.iso;*.cci;*.cso;*.zar|Tous les fichiers (*.*)|*.*</string>
    <string name="msg_no_input_files">Aucun fichier d'entrée sélectionné</string>
    <string name="msg_no_output_dir">Aucun dossier de sortie sélectionné</string>
    <string name="msg_no_valid_files">Aucun fichier valide trouvé dans l'emplacement sélectionné</string>

    <string name="tooltip_browse_input">Sélectionnez le fichier ou dossier à traiter</string>
    <string name="tooltip_browse_output">Sélectionnez le dossier de destination des fichiers traités</string>
    <string name="tooltip_fmt_iso">Crée une image XISO</string>
    <string name="tooltip_fmt_god">Crée une image Games on Demand (GoD)</string>
    <string name="tooltip_fmt_cci">Crée une archive compressée CCI</string>
    <string name="tooltip_fmt_cso">Crée une archive compressée CSO</string>
    <string name="tooltip_fmt_zar">Crée une archive compressée ZAR</string>
    <string name="tooltip_fmt_extract">Extrait tous les fichiers dans un dossier</string>
    <string name="tooltip_auto_ogxbox">Choisit automatiquement le format et réglages pour Xbox originale</string>
    <string name="tooltip_auto_xbox360">Choisit automatiquement le format et réglages pour Xbox 360</string>
    <string name="tooltip_auto_xemu">Choisit automatiquement le format et réglages pour Xemu</string>
    <string name="tooltip_auto_xenia">Choisit automatiquement le format et réglages pour Xenia</string>
    <string name="tooltip_scrub_none">Aucun nettoyage, seule la partition vidéo est retirée si présente</string>
    <string name="tooltip_scrub_partial">Nettoie et tronque l'image en supprimant les données de remplissage aléatoires</string>
    <string name="tooltip_scrub_full">Reconstruit entièrement l'image pour obtenir la taille minimale</string>
    <string name="tooltip_split">Divise le fichier XISO s'il dépasse la taille maximale pour OG Xbox</string>
    <string name="tooltip_attach_xbe">Génère un fichier attach XBE avec le fichier de sortie</string>
    <string name="tooltip_am_patch">Applique le patch au champ Allowed Media dans les fichiers XBE</string>
    <string name="tooltip_rename_xbe">Remplace le champ titre du XBE avec celui trouvé dans la base de données</string>
    <string name="tooltip_offline_mode">Désactive les fonctions en ligne, produisant des noms moins précis</string>
    <string name="tooltip_keep_name">Conserve le nom du fichier d'entrée d'origine (évite les écrasements pour les jeux multi-disques)</string>
    <string name="tooltip_lang_system">Utiliser la langue par défaut du système</string>
    <string name="tooltip_lang_english">Définir la langue sur Anglais</string>
    <string name="tooltip_lang_italian">Définir la langue sur Italien</string>
    <string name="tooltip_lang_german">Définir la langue sur Allemand</string>
    <string name="tooltip_lang_french">Définir la langue sur Français</string>
    <string name="tooltip_lang_spanish">Définir la langue sur Espagnol</string>
    <string name="tooltip_lang_portuguese">Définir la langue sur Portugais</string>
    <string name="tooltip_process_all">Traiter tous les fichiers de la liste</string>
    <string name="tooltip_pause">Mettre en pause le traitement</string>
    <string name="tooltip_cancel">Le traitement s'arrêtera après la fin du fichier en cours</string>

    <string name="cli_opt_input_path">Chemin d'entrée</string>
    <string name="cli_opt_output_dir">Dossier de sortie</string>
    <string name="cli_group_output_format">Format de Sortie</string>
    <string name="cli_group_settings">Paramètres</string>
    <string name="cli_flag_extract">Extrait tous les fichiers dans un dossier</string>
    <string name="cli_flag_xiso">Crée une image XISO</string>
    <string name="cli_flag_god">Crée une image Games on Demand (GoD)</string>
    <string name="cli_flag_cci">Crée une archive compressée CCI</string>
    <string name="cli_flag_cso">Crée une archive compressée CSO</string>
    <string name="cli_flag_zar">Crée une archive compressée ZAR</string>
    <string name="cli_flag_xbe">Génère un fichier attach XBE</string>
    <string name="cli_flag_ogxbox">Choisit automatiquement le format et réglages pour OG Xbox</string>
    <string name="cli_flag_xbox360">Choisit automatiquement le format et réglages pour Xbox 360</string>
    <string name="cli_flag_xemu">Choisit automatiquement le format et réglages pour Xemu</string>
    <string name="cli_flag_xenia">Choisit automatiquement le format et réglages pour Xenia</string>
    <string name="cli_flag_list">Liste le contenu des fichiers dans l'image d'entrée</string>
    <string name="cli_flag_help">Affiche ce message d'aide et quitte</string>
    <string name="cli_flag_partial_scrub">Nettoie et tronque l'image en supprimant le padding aléatoire</string>
    <string name="cli_flag_full_scrub">Reconstruit entièrement l'image pour la plus petite taille possible</string>
    <string name="cli_flag_split">Divise le fichier XISO résultant si trop grand pour OG Xbox</string>
    <string name="cli_flag_rename">Patche le titre du fichier XBE avec celui de la base de données</string>
    <string name="cli_flag_attach_xbe">Génère un fichier attach XBE avec le fichier de sortie</string>
    <string name="cli_flag_am_patch">Patche le champ Allowed Media dans les fichiers XBE</string>
    <string name="cli_flag_offline">Désactive les fonctionnalités en ligne</string>
    <string name="cli_flag_keep_name">Conserve le nom du fichier d'origine au lieu du titre de la base de données</string>
    <string name="cli_flag_lang">Spécifie la langue de l'interface (ex. 'fr', 'en', 'it', 'de', 'es', 'pt', 'system')</string>
    <string name="cli_flag_debug">Active la journalisation de débogage</string>
    <string name="cli_flag_quiet">Désactive tous les journaux sauf avertissements et erreurs</string>

    <string name="cli_msg_input_not_exist">Le chemin d'entrée n'existe pas : {0}</string>
    <string name="cli_msg_failed_input">Échec du traitement de l'entrée : {0}</string>
    <string name="cli_msg_finished">Traitement des fichiers d'entrée terminé.</string>
    <string name="cli_msg_processing">Traitement en cours : {0}</string>
    <string name="cli_msg_success_created">Créé avec succès : {0}</string>
    <string name="cli_msg_files_in_image">Fichiers dans l'image :</string>

    <string name="stage_writing_zar">Écriture de l'archive ZAR</string>
    <string name="stage_writing_xiso">Écriture du XISO</string>
    <string name="stage_writing_god">Écriture des données GoD</string>
    <string name="stage_writing_cso">Écriture du fichier CSO</string>
    <string name="stage_writing_cci">Écriture de l'archive CCI</string>
    <string name="stage_extracting">Extraction des fichiers</string>
</resources>
)xml";

inline constexpr std::string_view XML_ES = R"xml(<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="app_name">XGDTool</string>
    <string name="notification_title">XGDTool - Procesamiento Completado</string>
    <string name="batch_completed_all">Conversión completada: {0} de {1} correctos</string>
    <string name="batch_completed_with_errors">Conversión completada: {0} de {1} correctos, {2} fallidos</string>
    
    <string name="dialog_title_success">Procesamiento Completado</string>
    <string name="dialog_title_warning">Procesamiento Completado con Errores</string>
    <string name="dialog_msg_all_ok">¡Todos los {0} archivos se han procesado correctamente!</string>
    <string name="dialog_msg_single_ok">¡Archivo procesado correctamente!</string>
    <string name="dialog_msg_errors">Procesamiento finalizado con errores:\n\n• Correctos: {0}\n• Fallidos: {1}\n\nConsulta el archivo de registro para más detalles.</string>
    <string name="dialog_msg_cancelled">Procesamiento cancelado por el usuario.\n\n• Completados: {0}\n• Interrumpidos/Fallidos: {1}</string>
    
    <string name="btn_open_log">Abrir Archivo de Registro</string>
    <string name="btn_close">Cerrar</string>
    <string name="btn_ok">Aceptar</string>

    <string name="label_input_path">Ruta de entrada:</string>
    <string name="label_output_dir">Carpeta de salida:</string>
    <string name="label_file_list">Lista de archivos:</string>
    <string name="btn_browse">Examinar</string>
    <string name="col_format">Formato</string>
    <string name="col_filename">Nombre de archivo</string>
    <string name="label_status">Estado:</string>
    <string name="label_current_progress">Progreso actual:</string>
    <string name="label_total_progress">Progreso total:</string>

    <string name="section_output_format">Formato de salida:</string>
    <string name="section_scrub">Limpieza (Scrub):</string>
    <string name="section_settings">Ajustes:</string>
    <string name="section_language">Idioma:</string>

    <string name="scrub_none">Ninguno</string>
    <string name="scrub_partial">Parcial</string>
    <string name="scrub_full">Completo</string>

    <string name="setting_split">Dividir XISO</string>
    <string name="setting_attach_xbe">Generar Attach XBE</string>
    <string name="setting_am_patch">Parchear Allowed Media XBE</string>
    <string name="setting_rename_xbe">Renombrar título XBE</string>
    <string name="setting_offline_mode">Modo sin conexión</string>
    <string name="setting_keep_name">Mantener nombre original</string>

    <string name="lang_system">Sistema</string>
    <string name="lang_english">English</string>
    <string name="lang_italian">Italiano</string>
    <string name="lang_german">Deutsch</string>
    <string name="lang_french">Français</string>
    <string name="lang_spanish">Español</string>
    <string name="lang_portuguese">Português</string>

    <string name="btn_process_all">Procesar Todo</string>
    <string name="btn_pause">Pausa</string>
    <string name="btn_resume">Reanudar</string>
    <string name="btn_cancel">Cancelar</string>

    <string name="status_idle">En espera</string>
    <string name="status_paused">En pausa</string>
    <string name="status_processing">Procesando archivos</string>
    <string name="status_complete">Procesamiento completado</string>
    <string name="status_cancelled">Procesamiento cancelado</string>

    <string name="choose_selection_type_title">Elige el tipo de selección:</string>
    <string name="choose_selection_type_caption">Seleccionar</string>
    <string name="choice_select_files">Seleccionar Archivo(s)</string>
    <string name="choice_select_dir">Seleccionar Carpeta</string>
    <string name="dialog_select_files_title">Seleccionar uno o más archivos</string>
    <string name="dialog_select_dir_title">Seleccionar una carpeta</string>
    <string name="dialog_select_out_dir_title">Seleccionar carpeta de destino (GoD/Juego/Batch)</string>
    <string name="wildcard_xbox_images">Archivos de imagen Xbox (*.iso;*.cci;*.cso;*.zar)|*.iso;*.cci;*.cso;*.zar|Todos los archivos (*.*)|*.*</string>
    <string name="msg_no_input_files">No se seleccionó ningún archivo de entrada</string>
    <string name="msg_no_output_dir">No se seleccionó ninguna carpeta de salida</string>
    <string name="msg_no_valid_files">No se encontraron archivos válidos en la ruta seleccionada</string>

    <string name="tooltip_browse_input">Selecciona el archivo o carpeta de entrada a procesar</string>
    <string name="tooltip_browse_output">Selecciona la carpeta donde guardar los archivos procesados</string>
    <string name="tooltip_fmt_iso">Crea una imagen XISO</string>
    <string name="tooltip_fmt_god">Crea una imagen Games on Demand (GoD)</string>
    <string name="tooltip_fmt_cci">Crea un archivo comprimido CCI</string>
    <string name="tooltip_fmt_cso">Crea un archivo comprimido CSO</string>
    <string name="tooltip_fmt_zar">Crea un archivo comprimido ZAR</string>
    <string name="tooltip_fmt_extract">Extrae todos los archivos a una carpeta</string>
    <string name="tooltip_auto_ogxbox">Elige automáticamente formato y ajustes para Xbox original</string>
    <string name="tooltip_auto_xbox360">Elige automáticamente formato y ajustes para Xbox 360</string>
    <string name="tooltip_auto_xemu">Elige automáticamente formato y ajustes para Xemu</string>
    <string name="tooltip_auto_xenia">Elige automáticamente formato y ajustes para Xenia</string>
    <string name="tooltip_scrub_none">Sin limpieza, solo se elimina la partición de video si existe</string>
    <string name="tooltip_scrub_partial">Limpia y recorta la imagen, eliminando datos de relleno aleatorios</string>
    <string name="tooltip_scrub_full">Reconstruye totalmente la imagen para producir el tamaño más reducido posible</string>
    <string name="tooltip_split">Divide el archivo XISO si supera el tamaño máximo para OG Xbox</string>
    <string name="tooltip_attach_xbe">Genera un archivo attach XBE junto con el archivo de salida</string>
    <string name="tooltip_am_patch">Parchea el campo Allowed Media en los archivos XBE resultantes</string>
    <string name="tooltip_rename_xbe">Reemplaza el campo de título de los XBE con el de la base de datos</string>
    <string name="tooltip_offline_mode">Desactiva las funciones en línea, generando nombres menos precisos</string>
    <string name="tooltip_keep_name">Mantiene el nombre del archivo de entrada original (evita sobrescrituras en juegos multidisco)</string>
    <string name="tooltip_lang_system">Usar el idioma predeterminado del sistema</string>
    <string name="tooltip_lang_english">Establecer el idioma en Inglés</string>
    <string name="tooltip_lang_italian">Establecer el idioma en Italiano</string>
    <string name="tooltip_lang_german">Establecer el idioma en Alemán</string>
    <string name="tooltip_lang_french">Establecer el idioma en Francés</string>
    <string name="tooltip_lang_spanish">Establecer el idioma en Español</string>
    <string name="tooltip_lang_portuguese">Establecer el idioma en Portugués</string>
    <string name="tooltip_process_all">Procesar todos los archivos de la lista</string>
    <string name="tooltip_pause">Pausa el procesamiento de archivos</string>
    <string name="tooltip_cancel">El procesamiento se detendrá tras finalizar el archivo actual</string>

    <string name="cli_opt_input_path">Ruta de entrada</string>
    <string name="cli_opt_output_dir">Carpeta de salida</string>
    <string name="cli_group_output_format">Formato de Salida</string>
    <string name="cli_group_settings">Ajustes</string>
    <string name="cli_flag_extract">Extrae todos los archivos a una carpeta</string>
    <string name="cli_flag_xiso">Crea una imagen XISO</string>
    <string name="cli_flag_god">Crea una imagen Games on Demand (GoD)</string>
    <string name="cli_flag_cci">Crea un archivo comprimido CCI</string>
    <string name="cli_flag_cso">Crea un archivo comprimido CSO</string>
    <string name="cli_flag_zar">Crea un archivo comprimido ZAR</string>
    <string name="cli_flag_xbe">Genera un archivo attach XBE</string>
    <string name="cli_flag_ogxbox">Elige automáticamente formato y ajustes para OG Xbox</string>
    <string name="cli_flag_xbox360">Elige automáticamente formato y ajustes para Xbox 360</string>
    <string name="cli_flag_xemu">Elige automáticamente formato y ajustes para Xemu</string>
    <string name="cli_flag_xenia">Elige automáticamente formato y ajustes para Xenia</string>
    <string name="cli_flag_list">Enumera el contenido de archivos en la imagen de entrada</string>
    <string name="cli_flag_help">Muestra este mensaje de ayuda y sale</string>
    <string name="cli_flag_partial_scrub">Limpia y recorta la imagen, eliminando relleno aleatorio</string>
    <string name="cli_flag_full_scrub">Reconstruye totalmente la imagen para el tamaño mínimo</string>
    <string name="cli_flag_split">Divide el archivo XISO si es demasiado grande para OG Xbox</string>
    <string name="cli_flag_rename">Parchea el título del XBE con el nombre de la base de datos</string>
    <string name="cli_flag_attach_xbe">Genera un archivo attach XBE junto al archivo de salida</string>
    <string name="cli_flag_am_patch">Parchea el campo Allowed Media en los archivos XBE</string>
    <string name="cli_flag_offline">Desactiva las funciones en línea</string>
    <string name="cli_flag_keep_name">Mantiene el nombre del archivo de entrada original para la salida</string>
    <string name="cli_flag_lang">Especifica el idioma de la interfaz (ej. 'es', 'en', 'it', 'de', 'fr', 'pt', 'system')</string>
    <string name="cli_flag_debug">Habilita el registro de depuración</string>
    <string name="cli_flag_quiet">Desactiva todos los registros excepto advertencias y errores</string>

    <string name="cli_msg_input_not_exist">La ruta de entrada no existe: {0}</string>
    <string name="cli_msg_failed_input">Error al procesar la entrada: {0}</string>
    <string name="cli_msg_finished">Procesamiento de archivos de entrada finalizado.</string>
    <string name="cli_msg_processing">Procesando: {0}</string>
    <string name="cli_msg_success_created">Creado con éxito: {0}</string>
    <string name="cli_msg_files_in_image">Archivos en la imagen:</string>

    <string name="stage_writing_zar">Escribiendo archivo ZAR</string>
    <string name="stage_writing_xiso">Escribiendo XISO</string>
    <string name="stage_writing_god">Escribiendo datos GoD</string>
    <string name="stage_writing_cso">Escribiendo archivo CSO</string>
    <string name="stage_writing_cci">Escribiendo archivo CCI</string>
    <string name="stage_extracting">Extrayendo archivos</string>
</resources>
)xml";

inline constexpr std::string_view XML_PT = R"xml(<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="app_name">XGDTool</string>
    <string name="notification_title">XGDTool - Processamento Concluído</string>
    <string name="batch_completed_all">Conversão concluída: {0} de {1} com sucesso</string>
    <string name="batch_completed_with_errors">Conversão concluída: {0} de {1} com sucesso, {2} falharam</string>
    
    <string name="dialog_title_success">Processamento Concluído</string>
    <string name="dialog_title_warning">Processamento Concluído com Erros</string>
    <string name="dialog_msg_all_ok">Todos os {0} arquivos foram processados com sucesso!</string>
    <string name="dialog_msg_single_ok">Arquivo processado com sucesso!</string>
    <string name="dialog_msg_errors">Processamento finalizado com erros:\n\n• Sucesso: {0}\n• Falhas: {1}\n\nConsulte o arquivo de registro para mais detalhes.</string>
    <string name="dialog_msg_cancelled">Processamento cancelado pelo utilizador.\n\n• Concluídos: {0}\n• Interrompidos/Falhados: {1}</string>
    
    <string name="btn_open_log">Abrir Arquivo de Registo</string>
    <string name="btn_close">Fechar</string>
    <string name="btn_ok">OK</string>

    <string name="label_input_path">Caminho de entrada:</string>
    <string name="label_output_dir">Pasta de saída:</string>
    <string name="label_file_list">Lista de ficheiros:</string>
    <string name="btn_browse">Procurar</string>
    <string name="col_format">Formato</string>
    <string name="col_filename">Nome do ficheiro</string>
    <string name="label_status">Estado:</string>
    <string name="label_current_progress">Progresso atual:</string>
    <string name="label_total_progress">Progresso total:</string>

    <string name="section_output_format">Formato de saída:</string>
    <string name="section_scrub">Limpeza (Scrub):</string>
    <string name="section_settings">Definições:</string>
    <string name="section_language">Idioma:</string>

    <string name="scrub_none">Nenhum</string>
    <string name="scrub_partial">Parcial</string>
    <string name="scrub_full">Completo</string>

    <string name="setting_split">Dividir XISO</string>
    <string name="setting_attach_xbe">Gerar Attach XBE</string>
    <string name="setting_am_patch">Corrigir Allowed Media XBE</string>
    <string name="setting_rename_xbe">Renomear título XBE</string>
    <string name="setting_offline_mode">Modo offline</string>
    <string name="setting_keep_name">Manter nome original</string>

    <string name="lang_system">Sistema</string>
    <string name="lang_english">English</string>
    <string name="lang_italian">Italiano</string>
    <string name="lang_german">Deutsch</string>
    <string name="lang_french">Français</string>
    <string name="lang_spanish">Español</string>
    <string name="lang_portuguese">Português</string>

    <string name="btn_process_all">Processar Tudo</string>
    <string name="btn_pause">Pausa</string>
    <string name="btn_resume">Retomar</string>
    <string name="btn_cancel">Cancelar</string>

    <string name="status_idle">Em espera</string>
    <string name="status_paused">Em pausa</string>
    <string name="status_processing">A processar ficheiros</string>
    <string name="status_complete">Processamento concluído</string>
    <string name="status_cancelled">Processamento cancelado</string>

    <string name="choose_selection_type_title">Escolha o tipo de seleção:</string>
    <string name="choose_selection_type_caption">Selecionar</string>
    <string name="choice_select_files">Selecionar Ficheiro(s)</string>
    <string name="choice_select_dir">Selecionar Pasta</string>
    <string name="dialog_select_files_title">Selecionar um ou mais ficheiros</string>
    <string name="dialog_select_dir_title">Selecionar uma pasta</string>
    <string name="dialog_select_out_dir_title">Selecionar pasta de destino (GoD/Jogo/Batch)</string>
    <string name="wildcard_xbox_images">Ficheiros de imagem Xbox (*.iso;*.cci;*.cso;*.zar)|*.iso;*.cci;*.cso;*.zar|Todos os ficheiros (*.*)|*.*</string>
    <string name="msg_no_input_files">Nenhum ficheiro de entrada selecionado</string>
    <string name="msg_no_output_dir">Nenhuma pasta de saída selecionada</string>
    <string name="msg_no_valid_files">Nenhum ficheiro válido encontrado no caminho selecionado</string>

    <string name="tooltip_browse_input">Selecione o ficheiro ou pasta de entrada a processar</string>
    <string name="tooltip_browse_output">Selecione a pasta onde guardar os ficheiros processados</string>
    <string name="tooltip_fmt_iso">Cria uma imagem XISO</string>
    <string name="tooltip_fmt_god">Cria uma imagem Games on Demand (GoD)</string>
    <string name="tooltip_fmt_cci">Cria um arquivo comprimido CCI</string>
    <string name="tooltip_fmt_cso">Cria um arquivo comprimido CSO</string>
    <string name="tooltip_fmt_zar">Cria um arquivo comprimido ZAR</string>
    <string name="tooltip_fmt_extract">Extrai todos os ficheiros para uma pasta</string>
    <string name="tooltip_auto_ogxbox">Escolhe automaticamente formato e opções para Xbox original</string>
    <string name="tooltip_auto_xbox360">Escolhe automaticamente formato e opções para Xbox 360</string>
    <string name="tooltip_auto_xemu">Escolhe automaticamente formato e opções para Xemu</string>
    <string name="tooltip_auto_xenia">Escolhe automaticamente formato e opções para Xenia</string>
    <string name="tooltip_scrub_none">Sem limpeza, apenas a partição de vídeo é removida se presente</string>
    <string name="tooltip_scrub_partial">Limpa e apara a imagem, removendo dados de preenchimento aleatórios</string>
    <string name="tooltip_scrub_full">Reconstrói totalmente a imagem para obter o menor tamanho possível</string>
    <string name="tooltip_split">Divide o ficheiro XISO se exceder o tamanho máximo para OG Xbox</string>
    <string name="tooltip_attach_xbe">Gera um ficheiro attach XBE juntamente com o ficheiro de saída</string>
    <string name="tooltip_am_patch">Aplica o patch ao campo Allowed Media nos ficheiros XBE</string>
    <string name="tooltip_rename_xbe">Substitui o campo de título dos XBE pelo nome da base de dados</string>
    <string name="tooltip_offline_mode">Desativa funcionalidades online, gerando nomes menos precisos</string>
    <string name="tooltip_keep_name">Mantém o nome do ficheiro de entrada original (evita substituições em jogos multi-disco)</string>
    <string name="tooltip_lang_system">Utilizar o idioma predefinido do sistema</string>
    <string name="tooltip_lang_english">Definir o idioma para Inglês</string>
    <string name="tooltip_lang_italian">Definir o idioma para Italiano</string>
    <string name="tooltip_lang_german">Definir o idioma para Alemão</string>
    <string name="tooltip_lang_french">Definir o idioma para Francês</string>
    <string name="tooltip_lang_spanish">Definir o idioma para Espanhol</string>
    <string name="tooltip_lang_portuguese">Definir o idioma para Português</string>
    <string name="tooltip_process_all">Processar todos os ficheiros da lista</string>
    <string name="tooltip_pause">Pausa o processamento de ficheiros</string>
    <string name="tooltip_cancel">O processamento irá parar após o ficheiro atual</string>

    <string name="cli_opt_input_path">Caminho de entrada</string>
    <string name="cli_opt_output_dir">Pasta de saída</string>
    <string name="cli_group_output_format">Formato de Saída</string>
    <string name="cli_group_settings">Definições</string>
    <string name="cli_flag_extract">Extrai todos os ficheiros para uma pasta</string>
    <string name="cli_flag_xiso">Cria uma imagem XISO</string>
    <string name="cli_flag_god">Cria uma imagem Games on Demand (GoD)</string>
    <string name="cli_flag_cci">Cria um arquivo comprimido CCI</string>
    <string name="cli_flag_cso">Cria um arquivo comprimido CSO</string>
    <string name="cli_flag_zar">Cria um arquivo comprimido ZAR</string>
    <string name="cli_flag_xbe">Gera um ficheiro attach XBE</string>
    <string name="cli_flag_ogxbox">Escolhe automaticamente formato e opções para OG Xbox</string>
    <string name="cli_flag_xbox360">Escolhe automaticamente formato e opções para Xbox 360</string>
    <string name="cli_flag_xemu">Escolhe automaticamente formato e opções para Xemu</string>
    <string name="cli_flag_xenia">Escolhe automaticamente formato e opções para Xenia</string>
    <string name="cli_flag_list">Lista o conteúdo dos ficheiros na imagem de entrada</string>
    <string name="cli_flag_help">Apresenta esta mensagem de ajuda e sai</string>
    <string name="cli_flag_partial_scrub">Limpa e apara a imagem, removendo preenchimento aleatório</string>
    <string name="cli_flag_full_scrub">Reconstrói totalmente a imagem para o tamanho mínimo</string>
    <string name="cli_flag_split">Divide o ficheiro XISO se for demasiado grande para OG Xbox</string>
    <string name="cli_flag_rename">Aplica patch ao título do XBE com o nome da base de dados</string>
    <string name="cli_flag_attach_xbe">Gera um ficheiro attach XBE juntamente com o ficheiro de saída</string>
    <string name="cli_flag_am_patch">Aplica patch ao campo Allowed Media nos ficheiros XBE</string>
    <string name="cli_flag_offline">Desativa funcionalidades online</string>
    <string name="cli_flag_keep_name">Mantém o nome do ficheiro de entrada original</string>
    <string name="cli_flag_lang">Especifica o idioma da interface (ex. 'pt', 'en', 'it', 'de', 'fr', 'es', 'system')</string>
    <string name="cli_flag_debug">Ativa o registo de depuração</string>
    <string name="cli_flag_quiet">Desativa todos os registos exceto avisos e erros</string>

    <string name="cli_msg_input_not_exist">O caminho de entrada não existe: {0}</string>
    <string name="cli_msg_failed_input">Falha ao processar a entrada: {0}</string>
    <string name="cli_msg_finished">Processamento dos ficheiros de entrada concluído.</string>
    <string name="cli_msg_processing">A processar: {0}</string>
    <string name="cli_msg_success_created">Criado com sucesso: {0}</string>
    <string name="cli_msg_files_in_image">Ficheiros na imagem:</string>

    <string name="stage_writing_zar">A escrever arquivo ZAR</string>
    <string name="stage_writing_xiso">A escrever XISO</string>
    <string name="stage_writing_god">A escrever dados GoD</string>
    <string name="stage_writing_cso">A escrever ficheiro CSO</string>
    <string name="stage_writing_cci">A escrever arquivo CCI</string>
    <string name="stage_extracting">A extrair ficheiros</string>
</resources>
)xml";

inline std::string_view get(std::string_view lang)
{
    if (lang == "it") return XML_IT;
    if (lang == "de") return XML_DE;
    if (lang == "fr") return XML_FR;
    if (lang == "es") return XML_ES;
    if (lang == "pt") return XML_PT;
    if (lang == "en") return XML_EN;
    return {};
}

} // namespace EmbeddedLanguages
