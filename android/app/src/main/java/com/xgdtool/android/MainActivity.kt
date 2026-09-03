package com.xgdtool.android

import android.content.ClipData
import android.content.ClipboardManager
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.ServiceConnection
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.IBinder
import android.provider.OpenableColumns
import android.view.View
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import com.xgdtool.android.databinding.ActivityMainBinding
import com.xgdtool.android.databinding.ItemQueueFileBinding

data class QueueItem(
    val uri: Uri,
    val name: String,
    val size: Long,
    var status: String = ""
)

class MainActivity : AppCompatActivity(), ConvertServiceListener {

    private lateinit var binding: ActivityMainBinding

    private val queueItems = mutableListOf<QueueItem>()
    private var selectedOutputTree: Uri? = null

    private var service: ConvertService? = null
    private var bound = false

    private var isPathsExpanded = true
    private var isQueueExpanded = false
    private var isAdvancedExpanded = false
    private var isLogExpanded = true

    private val connection = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName?, binder: IBinder?) {
            service = (binder as ConvertService.LocalBinder).getService()
            service?.setListener(this@MainActivity)
            bound = true
            refreshRunningState()
        }
        override fun onServiceDisconnected(name: ComponentName?) {
            service = null
            bound = false
        }
    }

    private val pickFiles = registerForActivityResult(ActivityResultContracts.OpenMultipleDocuments()) { uris ->
        if (uris.isNotEmpty()) {
            uris.forEach { uri ->
                contentResolver.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION)
                if (queueItems.none { it.uri == uri }) {
                    val (name, size) = queryFileInfo(uri)
                    queueItems.add(QueueItem(uri, name, size, getString(R.string.queue_status_waiting)))
                }
            }
            refreshQueueUi()
            updatePathsUi(autoCollapseIfReady = true)

            // Auto-expand queue when files are added
            if (!isQueueExpanded && queueItems.isNotEmpty()) {
                toggleQueue(true)
            }
        }
    }

    private val pickOutputTree = registerForActivityResult(ActivityResultContracts.OpenDocumentTree()) { uri ->
        if (uri != null) {
            contentResolver.takePersistableUriPermission(
                uri,
                Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION
            )
            selectedOutputTree = uri
            updatePathsUi(autoCollapseIfReady = true)
        }
    }

    private val requestNotifPermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) { }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            requestNotifPermission.launch(android.Manifest.permission.POST_NOTIFICATIONS)
        }

        setupPathsCard()
        setupQueueCard()
        setupOptionsCard()
        setupLogCard()
        setupActionButtons()

        val intent = Intent(this, ConvertService::class.java)
        bindService(intent, connection, Context.BIND_AUTO_CREATE)
    }

    override fun onDestroy() {
        super.onDestroy()
        if (bound) {
            service?.setListener(null)
            unbindService(connection)
        }
    }

    // --- Paths UI ---

    private fun setupPathsCard() {
        binding.headerPaths.setOnClickListener {
            togglePaths(!isPathsExpanded)
        }
        binding.cardSource.setOnClickListener {
            pickFiles.launch(arrayOf("*/*"))
        }
        binding.btnSelectFiles.setOnClickListener {
            pickFiles.launch(arrayOf("*/*"))
        }
        binding.cardDestination.setOnClickListener {
            pickOutputTree.launch(null)
        }
        binding.btnSelectOutput.setOnClickListener {
            pickOutputTree.launch(null)
        }
        updatePathsUi(autoCollapseIfReady = false)
    }

    private fun togglePaths(expand: Boolean) {
        isPathsExpanded = expand
        binding.pathsContentLayout.visibility = if (expand) View.VISIBLE else View.GONE
        binding.iconTogglePaths.setImageResource(
            if (expand) R.drawable.ic_expand_less else R.drawable.ic_expand_more
        )
    }

    private fun updatePathsUi(autoCollapseIfReady: Boolean) {
        val fileCount = queueItems.size
        binding.inputSummary.text = if (fileCount > 0) {
            resources.getQuantityString(R.plurals.input_files_selected, fileCount, fileCount)
        } else {
            getString(R.string.input_summary_none)
        }

        val outSegment = selectedOutputTree?.lastPathSegment
        val cleanOutName = outSegment?.substringAfterLast(':')?.substringAfterLast('/') ?: outSegment

        binding.outputSummary.text = if (cleanOutName != null) {
            cleanOutName
        } else {
            getString(R.string.output_summary_none)
        }

        if (fileCount > 0 && cleanOutName != null) {
            binding.pathsSummaryText.text = getString(R.string.paths_summary_ready, fileCount, cleanOutName)
            if (autoCollapseIfReady && isPathsExpanded) {
                togglePaths(false)
            }
        } else if (fileCount > 0) {
            binding.pathsSummaryText.text = getString(R.string.paths_summary_partial, fileCount)
        } else {
            binding.pathsSummaryText.text = getString(R.string.paths_summary_none)
        }
    }

    // --- Queue UI ---

    private fun setupQueueCard() {
        binding.headerQueue.setOnClickListener {
            toggleQueue(!isQueueExpanded)
        }
        binding.btnClearQueue.setOnClickListener {
            queueItems.clear()
            refreshQueueUi()
            updatePathsUi(autoCollapseIfReady = false)
        }
        refreshQueueUi()
    }

    private fun toggleQueue(expand: Boolean) {
        isQueueExpanded = expand
        binding.queueContentLayout.visibility = if (expand) View.VISIBLE else View.GONE
        binding.iconToggleQueue.setImageResource(
            if (expand) R.drawable.ic_expand_less else R.drawable.ic_expand_more
        )
    }

    private fun refreshQueueUi() {
        val count = queueItems.size
        binding.queueTitleText.text = if (count > 0) {
            "${getString(R.string.section_queue)} ($count)"
        } else {
            getString(R.string.section_queue)
        }

        binding.btnClearQueue.visibility = if (count > 0) View.VISIBLE else View.GONE
        binding.queueEmptyText.visibility = if (count == 0) View.VISIBLE else View.GONE
        binding.queueItemsContainer.removeAllViews()

        queueItems.forEachIndexed { index, item ->
            val itemBinding = ItemQueueFileBinding.inflate(layoutInflater, binding.queueItemsContainer, false)
            itemBinding.itemFileName.text = item.name
            itemBinding.itemFileSize.text = formatBytesUi(item.size)

            if (item.status.isNotEmpty()) {
                itemBinding.itemStatusBadge.visibility = View.VISIBLE
                itemBinding.itemStatusBadge.text = item.status
            } else {
                itemBinding.itemStatusBadge.visibility = View.GONE
            }

            val running = service?.isRunning() == true
            itemBinding.btnRemoveItem.isEnabled = !running
            itemBinding.btnRemoveItem.setOnClickListener {
                queueItems.removeAt(index)
                refreshQueueUi()
                updatePathsUi(autoCollapseIfReady = false)
            }

            binding.queueItemsContainer.addView(itemBinding.root)
        }
    }

    // --- Options & Advanced UI ---

    private fun setupOptionsCard() {
        binding.btnToggleAdvanced.setOnClickListener {
            isAdvancedExpanded = !isAdvancedExpanded
            binding.advancedSettingsLayout.visibility = if (isAdvancedExpanded) View.VISIBLE else View.GONE
        }

        binding.formatGroup.setOnCheckedStateChangeListener { _, _ ->
            updateFormatDescription()
        }
        updateFormatDescription()
    }

    private fun updateFormatDescription() {
        val desc = when (binding.formatGroup.checkedChipId) {
            binding.formatGod.id -> "GOD (Games on Demand): Formato pacchetto originale per Xbox 360 (cartella Content/0000000000000000)"
            binding.formatCci.id -> "CCI: Formato compresso LZ4 ad alte prestazioni per emulatori"
            binding.formatCso.id -> "CSO: Formato compresso ISO (LZ4) ad ampia compatibilità"
            binding.formatIso.id -> "ISO: Immagine disco standard XISO decrittata e scrubbata"
            binding.formatDir.id -> "Estrai (HDD): Estrazione diretta dei file su cartella per hard disk interno o USB"
            else -> "ZAR: Archivio compresso universale Zstandard ultra-veloce (compatibile Cemu/emulatori)"
        }
        binding.formatDescription.text = desc
    }

    // --- Log UI ---

    private fun setupLogCard() {
        binding.headerLog.setOnClickListener {
            isLogExpanded = !isLogExpanded
            binding.logContentLayout.visibility = if (isLogExpanded) View.VISIBLE else View.GONE
            binding.iconToggleLog.setImageResource(
                if (isLogExpanded) R.drawable.ic_expand_less else R.drawable.ic_expand_more
            )
        }

        binding.btnCopyLog.setOnClickListener {
            val logText = binding.logView.text.toString()
            if (logText.isNotEmpty()) {
                val clipboard = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                val clip = ClipData.newPlainText("XGDTool Log", logText)
                clipboard.setPrimaryClip(clip)
                Toast.makeText(this, R.string.log_copied, Toast.LENGTH_SHORT).show()
            }
        }

        binding.btnClearLog.setOnClickListener {
            binding.logView.text = ""
            Toast.makeText(this, R.string.log_cleared, Toast.LENGTH_SHORT).show()
        }
    }

    // --- Actions & Conversion ---

    private fun setupActionButtons() {
        binding.btnConvert.setOnClickListener { startConversion() }
        binding.btnCancel.setOnClickListener { service?.requestCancel() }
    }

    private fun refreshRunningState() {
        val running = service?.isRunning() == true
        binding.btnConvert.isEnabled = !running
        binding.btnCancel.isEnabled = running
        binding.btnClearQueue.isEnabled = !running
        binding.btnSelectFiles.isEnabled = !running
        binding.btnSelectOutput.isEnabled = !running
    }

    private fun startConversion() {
        if (queueItems.isEmpty()) {
            binding.logView.append(getString(R.string.error_no_input) + "\n")
            Toast.makeText(this, R.string.error_no_input, Toast.LENGTH_SHORT).show()
            return
        }
        val outputTree = selectedOutputTree
        if (outputTree == null) {
            binding.logView.append(getString(R.string.error_no_output) + "\n")
            Toast.makeText(this, R.string.error_no_output, Toast.LENGTH_SHORT).show()
            return
        }

        val fileType = when (binding.formatGroup.checkedChipId) {
            binding.formatGod.id -> XgdNative.FORMAT_GOD
            binding.formatCci.id -> XgdNative.FORMAT_CCI
            binding.formatCso.id -> XgdNative.FORMAT_CSO
            binding.formatIso.id -> XgdNative.FORMAT_ISO
            binding.formatDir.id -> XgdNative.FORMAT_DIR
            else -> XgdNative.FORMAT_ZAR
        }

        val scrubType = when (binding.scrubGroup.checkedChipId) {
            binding.scrubPartial.id -> XgdNative.SCRUB_PARTIAL
            binding.scrubFull.id -> XgdNative.SCRUB_FULL
            else -> XgdNative.SCRUB_NONE
        }

        val compressionLevel = when (binding.compressionGroup.checkedChipId) {
            binding.compFast.id -> 1
            binding.compBalanced.id -> 3
            binding.compMax.id -> 6
            else -> 2
        }

        val split = binding.checkSplit.isChecked
        val attachXbe = binding.checkAttachXbe.isChecked
        val amPatch = binding.checkAmPatch.isChecked
        val offline = binding.checkOffline.isChecked

        val settings = ConversionSettings(
            fileType = fileType,
            scrubType = scrubType,
            offlineMode = offline,
            compressionLevel = compressionLevel,
            split = split,
            renameXbe = !offline,
            attachXbe = attachXbe,
            amPatch = amPatch
        )

        binding.logView.text = ""
        binding.progressBar.progress = 0

        // Reset queue status
        queueItems.forEach { it.status = getString(R.string.queue_status_waiting) }
        refreshQueueUi()

        val inputUris = queueItems.map { it.uri }
        val serviceIntent = Intent(this, ConvertService::class.java)
        startService(serviceIntent)
        service?.startQueue(inputUris, outputTree, settings)

        refreshRunningState()
    }

    // --- Helper to query file name and size ---

    private fun queryFileInfo(uri: Uri): Pair<String, Long> {
        var name = uri.lastPathSegment ?: "unknown.iso"
        var size = 0L
        try {
            contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE), null, null, null)?.use { cursor ->
                if (cursor.moveToFirst()) {
                    val nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                    val sizeIndex = cursor.getColumnIndex(OpenableColumns.SIZE)
                    if (nameIndex != -1) {
                        name = cursor.getString(nameIndex) ?: name
                    }
                    if (sizeIndex != -1) {
                        size = cursor.getLong(sizeIndex)
                    }
                }
            }
        } catch (_: Exception) { }
        return Pair(name, size)
    }

    private fun formatBytesUi(bytes: Long): String {
        if (bytes <= 0) return "0 B"
        val gb = bytes / (1024.0 * 1024.0 * 1024.0)
        return if (gb >= 0.1) String.format("%.2f GB", gb) else String.format("%.0f MB", bytes / (1024.0 * 1024.0))
    }

    // --- ConvertServiceListener ---

    override fun onQueueProgress(fileIndex: Int, fileCount: Int, currentFileName: String) {
        binding.progressLabel.text = getString(R.string.queue_progress, fileIndex, fileCount, currentFileName)
        binding.progressBar.progress = 0

        val itemIndex = fileIndex - 1
        if (itemIndex in queueItems.indices) {
            queueItems[itemIndex].status = getString(R.string.queue_status_converting)
            refreshQueueUi()
        }
    }

    override fun onPhase(phaseLabel: String, indeterminate: Boolean) {
        binding.phaseLabel.text = phaseLabel
        binding.progressBar.isIndeterminate = indeterminate
        if (!indeterminate) {
            binding.progressBar.progress = 0
        }
    }

    override fun onFileProgress(processed: Long, total: Long) {
        if (total > 0 && !binding.progressBar.isIndeterminate) {
            val pct = ((processed * 1000) / total).toInt()
            binding.progressBar.progress = pct
            binding.progressLabel.text = "${pct / 10}.${pct % 10}%  (${formatBytesUi(processed)} / ${formatBytesUi(total)})"
        }
    }

    override fun onLog(line: String) {
        binding.logView.append(line + "\n")
        binding.logScrollView.post {
            binding.logScrollView.fullScroll(View.FOCUS_DOWN)
        }
    }

    override fun onFinished(succeeded: Int, failed: Int, cancelled: Boolean) {
        val status = when {
            cancelled -> getString(R.string.finished_cancelled)
            failed == 0 -> resources.getQuantityString(R.plurals.finished_success, succeeded, succeeded)
            else -> getString(R.string.finished_partial, succeeded, failed)
        }
        binding.phaseLabel.text = status
        binding.progressBar.isIndeterminate = false
        binding.progressBar.progress = if (failed == 0 && !cancelled) 1000 else 0
        binding.logView.append("$status\n")

        // Mark items
        queueItems.forEach {
            if (it.status == getString(R.string.queue_status_converting)) {
                it.status = if (failed == 0 && !cancelled) getString(R.string.queue_status_done) else getString(R.string.queue_status_error)
            }
        }
        refreshQueueUi()
        refreshRunningState()
    }
}
