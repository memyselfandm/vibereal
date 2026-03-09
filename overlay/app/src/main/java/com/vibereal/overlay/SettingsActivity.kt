package com.vibereal.overlay

import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import com.vibereal.overlay.databinding.ActivitySettingsBinding
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.IOException
import java.util.concurrent.TimeUnit

class SettingsActivity : AppCompatActivity() {

    private lateinit var binding: ActivitySettingsBinding
    private lateinit var config: AppConfig
    private val scope = CoroutineScope(Dispatchers.Main + SupervisorJob())

    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(10, TimeUnit.SECONDS)
        .build()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivitySettingsBinding.inflate(layoutInflater)
        setContentView(binding.root)

        config = AppConfig.getInstance(this)
        populateFields()
        setupListeners()
    }

    override fun onDestroy() {
        super.onDestroy()
        scope.cancel()
    }

    // ---------------------------------------------------------------------------
    // Field population
    // ---------------------------------------------------------------------------

    private fun populateFields() {
        binding.editHubUrl.setText(config.hubUrl)
        binding.editHubApiKey.setText(config.hubApiKey)
        binding.editElevenLabsApiKey.setText(config.elevenLabsApiKey)
        binding.editVoiceId.setText(config.voiceId)
        binding.editModelId.setText(config.modelId)
    }

    // ---------------------------------------------------------------------------
    // Button listeners
    // ---------------------------------------------------------------------------

    private fun setupListeners() {
        binding.btnTestConnection.setOnClickListener {
            testConnection()
        }

        binding.btnSave.setOnClickListener {
            saveAndFinish()
        }
    }

    private fun testConnection() {
        val hubUrl = binding.editHubUrl.text.toString().trim()
            .replace("wss://", "https://")
            .replace("ws://", "http://")
            .trimEnd('/')

        val healthUrl = "$hubUrl/health"

        binding.btnTestConnection.isEnabled = false

        scope.launch {
            val result = withContext(Dispatchers.IO) {
                try {
                    val request = Request.Builder().url(healthUrl).get().build()
                    httpClient.newCall(request).execute().use { response ->
                        if (response.isSuccessful) {
                            "Connected: ${response.body?.string()}"
                        } else {
                            "Error: HTTP ${response.code}"
                        }
                    }
                } catch (e: IOException) {
                    "Connection failed: ${e.message}"
                } catch (e: IllegalArgumentException) {
                    "Invalid URL: $healthUrl"
                }
            }
            binding.btnTestConnection.isEnabled = true
            Toast.makeText(this@SettingsActivity, result, Toast.LENGTH_LONG).show()
        }
    }

    private fun saveAndFinish() {
        config.hubUrl = binding.editHubUrl.text.toString().trim()
        config.hubApiKey = binding.editHubApiKey.text.toString().trim()
        config.elevenLabsApiKey = binding.editElevenLabsApiKey.text.toString().trim()
        config.voiceId = binding.editVoiceId.text.toString().trim()
        config.modelId = binding.editModelId.text.toString().trim()

        Toast.makeText(this, "Settings saved", Toast.LENGTH_SHORT).show()
        finish()
    }
}
