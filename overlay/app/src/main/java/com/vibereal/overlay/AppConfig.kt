package com.vibereal.overlay

import android.content.Context
import android.content.SharedPreferences

class AppConfig private constructor(context: Context) {

    private val prefs: SharedPreferences =
        context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)

    var hubUrl: String
        get() = prefs.getString(KEY_HUB_URL, DEFAULT_HUB_URL) ?: DEFAULT_HUB_URL
        set(value) = prefs.edit().putString(KEY_HUB_URL, value).apply()

    var hubApiKey: String
        get() = prefs.getString(KEY_HUB_API_KEY, "") ?: ""
        set(value) = prefs.edit().putString(KEY_HUB_API_KEY, value).apply()

    var elevenLabsApiKey: String
        get() = prefs.getString(KEY_ELEVEN_LABS_API_KEY, "") ?: ""
        set(value) = prefs.edit().putString(KEY_ELEVEN_LABS_API_KEY, value).apply()

    var voiceId: String
        get() = prefs.getString(KEY_VOICE_ID, DEFAULT_VOICE_ID) ?: DEFAULT_VOICE_ID
        set(value) = prefs.edit().putString(KEY_VOICE_ID, value).apply()

    var modelId: String
        get() = prefs.getString(KEY_MODEL_ID, DEFAULT_MODEL_ID) ?: DEFAULT_MODEL_ID
        set(value) = prefs.edit().putString(KEY_MODEL_ID, value).apply()

    fun isConfigured(): Boolean = hubUrl.isNotEmpty()

    /**
     * Derives the overlay page URL from the stored hub HTTP URL.
     * Converts ws:// -> http:// and wss:// -> https:// if needed,
     * then appends /overlay as the path.
     */
    fun overlayPageUrl(): String {
        var base = hubUrl
            .replace("wss://", "https://")
            .replace("ws://", "http://")
        // Strip any path (e.g. /client) to get the base URL
        val schemeEnd = base.indexOf("://")
        if (schemeEnd >= 0) {
            val pathStart = base.indexOf('/', schemeEnd + 3)
            if (pathStart >= 0) base = base.substring(0, pathStart)
        }
        return "$base/overlay"
    }

    companion object {
        private const val PREFS_NAME = "vibereal_overlay_prefs"
        private const val KEY_HUB_URL = "hub_url"
        private const val KEY_HUB_API_KEY = "hub_api_key"
        private const val KEY_ELEVEN_LABS_API_KEY = "eleven_labs_api_key"
        private const val KEY_VOICE_ID = "eleven_labs_voice_id"
        private const val KEY_MODEL_ID = "eleven_labs_model_id"

        private const val DEFAULT_HUB_URL = "http://10.41.41.237:41237"
        private const val DEFAULT_VOICE_ID = "C92s6vssSLlabgIln1iY"
        private const val DEFAULT_MODEL_ID = "eleven_turbo_v2_5"

        @Volatile
        private var instance: AppConfig? = null

        fun getInstance(context: Context): AppConfig =
            instance ?: synchronized(this) {
                instance ?: AppConfig(context.applicationContext).also { instance = it }
            }
    }
}
