package com.vibereal.overlay

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Intent
import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioRecord
import android.media.AudioTrack
import android.media.MediaRecorder
import android.os.Binder
import android.os.IBinder
import android.util.Log
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream
import java.io.IOException

/**
 * Foreground service that owns the microphone and AudioRecord lifecycle.
 *
 * Activities bind to this service via [LocalBinder] and call [startRecording],
 * [stopRecording], and [speak] to drive voice interactions.
 */
class VoiceService : Service() {

    inner class LocalBinder : Binder() {
        fun getService(): VoiceService = this@VoiceService
    }

    private val binder = LocalBinder()
    private val scope = CoroutineScope(Dispatchers.Default + SupervisorJob())

    private var audioRecord: AudioRecord? = null
    private var recordingJob: Job? = null
    private val pcmBuffer = ByteArrayOutputStream()
    private var isRecording = false

    private var elevenLabsClient: ElevenLabsClient? = null

    // ---------------------------------------------------------------------------
    // Service lifecycle
    // ---------------------------------------------------------------------------

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
        startForeground(NOTIFICATION_ID, buildNotification())
    }

    override fun onBind(intent: Intent): IBinder = binder

    override fun onDestroy() {
        super.onDestroy()
        stopRecordingInternal()
        scope.cancel()
    }

    // ---------------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------------

    fun configure(config: AppConfig) {
        if (config.elevenLabsApiKey.isNotEmpty()) {
            elevenLabsClient = ElevenLabsClient(
                apiKey = config.elevenLabsApiKey,
                voiceId = config.voiceId,
                modelId = config.modelId,
            )
        }
    }

    /**
     * Starts capturing audio from the microphone into an internal PCM buffer.
     * A previous recording is discarded if one is already in progress.
     */
    fun startRecording() {
        stopRecordingInternal()
        pcmBuffer.reset()

        val minBuf = AudioRecord.getMinBufferSize(
            SAMPLE_RATE,
            AudioFormat.CHANNEL_IN_MONO,
            AudioFormat.ENCODING_PCM_16BIT,
        )
        val bufferSize = maxOf(minBuf, MIN_BUFFER_BYTES)

        val record = AudioRecord(
            MediaRecorder.AudioSource.VOICE_RECOGNITION,
            SAMPLE_RATE,
            AudioFormat.CHANNEL_IN_MONO,
            AudioFormat.ENCODING_PCM_16BIT,
            bufferSize,
        )

        if (record.state != AudioRecord.STATE_INITIALIZED) {
            Log.e(TAG, "AudioRecord failed to initialize")
            record.release()
            return
        }

        audioRecord = record
        isRecording = true
        record.startRecording()

        recordingJob = scope.launch {
            val chunk = ByteArray(bufferSize)
            while (isActive && isRecording) {
                val read = record.read(chunk, 0, chunk.size)
                if (read > 0) {
                    synchronized(pcmBuffer) {
                        pcmBuffer.write(chunk, 0, read)
                    }
                }
            }
        }

        Log.d(TAG, "Recording started")
    }

    /**
     * Stops audio capture, encodes the buffered PCM to WAV, and transcribes it
     * via ElevenLabs STT.
     *
     * @param onResult Called on the calling thread with the transcript. May be
     *   called with an empty string on error.
     */
    fun stopRecording(onResult: (String) -> Unit) {
        stopRecordingInternal()

        val pcmBytes = synchronized(pcmBuffer) { pcmBuffer.toByteArray() }
        pcmBuffer.reset()

        if (pcmBytes.isEmpty()) {
            Log.w(TAG, "stopRecording: no audio captured")
            onResult("")
            return
        }

        val client = elevenLabsClient
        if (client == null) {
            Log.w(TAG, "stopRecording: ElevenLabsClient not configured")
            onResult("")
            return
        }

        scope.launch {
            val transcript = try {
                val wavBytes = WavEncoder.encode(pcmBytes, SAMPLE_RATE)
                client.transcribe(wavBytes)
            } catch (e: IOException) {
                Log.e(TAG, "Transcription failed", e)
                ""
            }
            withContext(Dispatchers.Main) { onResult(transcript) }
        }
    }

    /**
     * Synthesizes [text] to speech via ElevenLabs TTS and plays it through
     * the device speaker.
     *
     * @param text         The text to speak.
     * @param onComplete   Called on the main thread after playback finishes (or on error).
     */
    fun speak(text: String, onComplete: () -> Unit) {
        val client = elevenLabsClient
        if (client == null) {
            Log.w(TAG, "speak: ElevenLabsClient not configured")
            onComplete()
            return
        }

        scope.launch {
            try {
                val pcmBytes = client.synthesize(text)
                playPcm(pcmBytes)
            } catch (e: IOException) {
                Log.e(TAG, "Speech synthesis failed", e)
            } finally {
                withContext(Dispatchers.Main) { onComplete() }
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Internal helpers
    // ---------------------------------------------------------------------------

    private fun stopRecordingInternal() {
        isRecording = false
        recordingJob?.cancel()
        recordingJob = null
        audioRecord?.apply {
            stop()
            release()
        }
        audioRecord = null
    }

    private fun playPcm(pcmBytes: ByteArray) {
        val minBuf = AudioTrack.getMinBufferSize(
            SAMPLE_RATE,
            AudioFormat.CHANNEL_OUT_MONO,
            AudioFormat.ENCODING_PCM_16BIT,
        )

        val track = AudioTrack.Builder()
            .setAudioAttributes(
                AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_MEDIA)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
                    .build(),
            )
            .setAudioFormat(
                AudioFormat.Builder()
                    .setSampleRate(SAMPLE_RATE)
                    .setChannelMask(AudioFormat.CHANNEL_OUT_MONO)
                    .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                    .build(),
            )
            .setBufferSizeInBytes(maxOf(minBuf, pcmBytes.size))
            .setTransferMode(AudioTrack.MODE_STATIC)
            .build()

        track.write(pcmBytes, 0, pcmBytes.size)
        track.play()

        // Block until playback completes before releasing
        val durationMs = (pcmBytes.size.toLong() * 1000L) / (SAMPLE_RATE * 2L)
        Thread.sleep(durationMs + 200)

        track.stop()
        track.release()
    }

    private fun createNotificationChannel() {
        val channel = NotificationChannel(
            CHANNEL_ID,
            "Voice Service",
            NotificationManager.IMPORTANCE_LOW,
        ).apply {
            description = "VibeReal voice capture service"
        }
        val manager = getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(channel)
    }

    private fun buildNotification(): Notification =
        Notification.Builder(this, CHANNEL_ID)
            .setContentTitle("VibeReal")
            .setContentText("Voice service active")
            .setSmallIcon(android.R.drawable.ic_btn_speak_now)
            .build()

    companion object {
        private const val TAG = "VoiceService"
        private const val CHANNEL_ID = "voice_channel"
        private const val NOTIFICATION_ID = 1001
        private const val SAMPLE_RATE = 16000
        private const val MIN_BUFFER_BYTES = 8192
    }
}
