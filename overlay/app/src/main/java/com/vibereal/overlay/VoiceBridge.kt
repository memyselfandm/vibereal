package com.vibereal.overlay

import android.util.Log
import android.webkit.JavascriptInterface
import android.webkit.WebView
import androidx.appcompat.app.AppCompatActivity

/**
 * JavaScript bridge that exposes voice capabilities to the WebView overlay page.
 *
 * Registered as the `Android` interface:
 *   `webView.addJavascriptInterface(bridge, "Android")`
 *
 * The WebView page communicates results back via:
 *   - `window.onTranscript(text)`   – called with the STT result
 *   - `window.onSpeechComplete()`   – called when TTS playback finishes
 *   - `window.onVoiceError(message)` – called on any voice error
 *
 * All callbacks are dispatched to the UI thread via [WebView.post].
 */
class VoiceBridge(
    private val activity: AppCompatActivity,
    private val webView: WebView,
) {
    // Injected after the Activity binds to VoiceService
    var voiceService: VoiceService? = null

    @JavascriptInterface
    fun startListening() {
        val service = voiceService
        if (service == null) {
            dispatchError("Voice service not available")
            return
        }
        try {
            service.startRecording()
        } catch (e: Exception) {
            Log.e(TAG, "startListening failed", e)
            dispatchError(e.message ?: "Unknown error")
        }
    }

    @JavascriptInterface
    fun stopListening() {
        val service = voiceService
        if (service == null) {
            dispatchError("Voice service not available")
            return
        }
        try {
            service.stopRecording { transcript ->
                dispatchTranscript(transcript)
            }
        } catch (e: Exception) {
            Log.e(TAG, "stopListening failed", e)
            dispatchError(e.message ?: "Unknown error")
        }
    }

    @JavascriptInterface
    fun speak(text: String) {
        val service = voiceService
        if (service == null) {
            dispatchError("Voice service not available")
            return
        }
        try {
            service.speak(text) {
                dispatchSpeechComplete()
            }
        } catch (e: Exception) {
            Log.e(TAG, "speak failed", e)
            dispatchError(e.message ?: "Unknown error")
        }
    }

    // ---------------------------------------------------------------------------
    // WebView callback dispatchers — always marshal to UI thread
    // ---------------------------------------------------------------------------

    private fun dispatchTranscript(text: String) {
        val escaped = text
            .replace("\\", "\\\\")
            .replace("'", "\\'")
            .replace("\n", "\\n")
            .replace("\r", "\\r")
        webView.post {
            webView.evaluateJavascript("window.onTranscript('$escaped')", null)
        }
    }

    private fun dispatchSpeechComplete() {
        webView.post {
            webView.evaluateJavascript("window.onSpeechComplete()", null)
        }
    }

    private fun dispatchError(message: String) {
        val escaped = message
            .replace("\\", "\\\\")
            .replace("'", "\\'")
        webView.post {
            webView.evaluateJavascript("window.onVoiceError('$escaped')", null)
        }
    }

    companion object {
        private const val TAG = "VoiceBridge"
    }
}
