package com.xgdtool.android

import android.content.Context
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.provider.DocumentsContract
import java.io.File

object PathUtils {

    /**
     * Checks if the app has All Files Access permission (Android 11+).
     */
    fun hasAllFilesAccess(): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            Environment.isExternalStorageManager()
        } else {
            true
        }
    }

    /**
     * Resolves a SAF document Uri (for a single file) to a real filesystem File if accessible.
     * Returns null if it cannot be resolved or accessed directly.
     */
    fun resolveDocumentFile(context: Context, uri: Uri): File? {
        if (!hasAllFilesAccess()) return null

        try {
            val docId = if (DocumentsContract.isDocumentUri(context, uri)) {
                DocumentsContract.getDocumentId(uri)
            } else {
                uri.path ?: return null
            }

            val file = resolveDocIdToFile(docId) ?: return null
            return if (file.exists() && file.canRead()) file else null
        } catch (_: Exception) {
            return null
        }
    }

    /**
     * Resolves a SAF tree Uri (for an output directory) to a real filesystem File if accessible.
     * Returns null if it cannot be resolved or accessed directly.
     */
    fun resolveTreeDir(context: Context, treeUri: Uri): File? {
        if (!hasAllFilesAccess()) return null

        try {
            val docId = try {
                DocumentsContract.getTreeDocumentId(treeUri)
            } catch (_: Exception) {
                if (DocumentsContract.isDocumentUri(context, treeUri)) {
                    DocumentsContract.getDocumentId(treeUri)
                } else {
                    treeUri.path ?: return null
                }
            }

            val dir = resolveDocIdToFile(docId) ?: return null
            if (!dir.exists()) {
                dir.mkdirs()
            }
            return if (dir.isDirectory && dir.canWrite()) dir else null
        } catch (_: Exception) {
            return null
        }
    }

    private fun resolveDocIdToFile(docId: String): File? {
        // e.g. "primary:Download/game.zar" or "primary:"
        if (docId.startsWith("primary:", ignoreCase = true)) {
            val relPath = docId.substringAfter(":", "")
            val root = Environment.getExternalStorageDirectory()
            return if (relPath.isEmpty()) root else File(root, relPath)
        }

        // Secondary SD Card or USB OTG: e.g. "1234-5678:Games/game.iso"
        if (docId.contains(":")) {
            val parts = docId.split(":", limit = 2)
            if (parts.size == 2) {
                val volumeId = parts[0]
                val relPath = parts[1]
                val storageDir = File("/storage", volumeId)
                if (storageDir.exists()) {
                    return if (relPath.isEmpty()) storageDir else File(storageDir, relPath)
                }
            }
        }

        // Direct raw path
        if (docId.startsWith("/")) {
            return File(docId)
        }

        return null
    }
}
