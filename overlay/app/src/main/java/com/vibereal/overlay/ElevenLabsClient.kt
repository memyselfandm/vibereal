package com.vibereal.overlay

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException
import java.util.concurrent.TimeUnit

/**
 * HTTP client for ElevenLabs speech-to-text and text-to-speech APIs.
 *
 * All network calls are performed on [Dispatchers.IO] using OkHttp's
 * synchronous [okhttp3.Call.execute].
 */
class ElevenLabsClient(
    private val apiKey: String,
    private val voiceId: String,
    private val modelId: String,
) {
    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .writeTimeout(30, TimeUnit.SECONDS)
        .build()

    /**
     * Transcribes audio using ElevenLabs Scribe.
     *
     * @param wavBytes A complete WAV file byte array.
     * @return The transcribed text string.
     * @throws IOException on network failure or non-2xx response.
     */
    suspend fun transcribe(wavBytes: ByteArray): String = withContext(Dispatchers.IO) {
        val requestBody = MultipartBody.Builder()
            .setType(MultipartBody.FORM)
            .addFormDataPart("model_id", "scribe_v1")
            .addFormDataPart(
                "file",
                "audio.wav",
                wavBytes.toRequestBody("audio/wav".toMediaType()),
            )
            .build()

        val request = Request.Builder()
            .url("https://api.elevenlabs.io/v1/speech-to-text")
            .addHeader("xi-api-key", apiKey)
            .post(requestBody)
            .build()

        httpClient.newCall(request).execute().use { response ->
            if (!response.isSuccessful) {
                throw IOException("STT request failed: ${response.code} ${response.message}")
            }
            val body = response.body?.string()
                ?: throw IOException("STT response body was empty")
            JSONObject(body).getString("text")
        }
    }

    /**
     * Synthesizes speech using ElevenLabs TTS, returning raw PCM audio at 16 kHz.
     *
     * @param text The text to synthesize.
     * @return Raw signed 16-bit little-endian PCM bytes at 16000 Hz mono.
     * @throws IOException on network failure or non-2xx response.
     */
    suspend fun synthesize(text: String): ByteArray = withContext(Dispatchers.IO) {
        val jsonBody = JSONObject().apply {
            put("text", text)
            put("model_id", modelId)
        }.toString()

        val request = Request.Builder()
            .url("https://api.elevenlabs.io/v1/text-to-speech/$voiceId?output_format=pcm_16000")
            .addHeader("Content-Type", "application/json")
            .addHeader("xi-api-key", apiKey)
            .post(jsonBody.toRequestBody("application/json".toMediaType()))
            .build()

        httpClient.newCall(request).execute().use { response ->
            if (!response.isSuccessful) {
                throw IOException("TTS request failed: ${response.code} ${response.message}")
            }
            response.body?.bytes()
                ?: throw IOException("TTS response body was empty")
        }
    }
}
