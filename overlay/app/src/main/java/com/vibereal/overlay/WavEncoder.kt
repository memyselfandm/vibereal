package com.vibereal.overlay

/**
 * Encodes raw 16-bit signed little-endian PCM bytes (as produced by Android's
 * AudioRecord) into a valid RIFF/WAVE file byte array.
 *
 * The WAV header layout mirrors the encoding used in ElevenLabsSTT.cs in the
 * Unity component of this project.
 */
object WavEncoder {

    /**
     * @param pcmData      Raw 16-bit signed LE PCM samples as produced by AudioRecord.
     * @param sampleRate   Sample rate in Hz (default 16000).
     * @param channels     Number of audio channels (default 1 = mono).
     * @param bitsPerSample Bits per sample (default 16).
     * @return Complete WAV byte array (44-byte header + PCM data).
     */
    fun encode(
        pcmData: ByteArray,
        sampleRate: Int = 16000,
        channels: Int = 1,
        bitsPerSample: Int = 16,
    ): ByteArray {
        val dataSize = pcmData.size
        val byteRate = sampleRate * channels * bitsPerSample / 8
        val blockAlign = channels * bitsPerSample / 8
        val fileSize = 44 + dataSize

        val wav = ByteArray(fileSize)
        var pos = 0

        // RIFF header
        writeString(wav, pos, "RIFF"); pos += 4
        writeInt32(wav, pos, fileSize - 8); pos += 4
        writeString(wav, pos, "WAVE"); pos += 4

        // fmt chunk
        writeString(wav, pos, "fmt "); pos += 4
        writeInt32(wav, pos, 16); pos += 4                          // chunk size
        writeInt16(wav, pos, 1); pos += 2                           // PCM format
        writeInt16(wav, pos, channels.toShort()); pos += 2
        writeInt32(wav, pos, sampleRate); pos += 4
        writeInt32(wav, pos, byteRate); pos += 4
        writeInt16(wav, pos, blockAlign.toShort()); pos += 2
        writeInt16(wav, pos, bitsPerSample.toShort()); pos += 2

        // data chunk
        writeString(wav, pos, "data"); pos += 4
        writeInt32(wav, pos, dataSize); pos += 4

        // Copy raw PCM bytes (already 16-bit signed LE from AudioRecord)
        System.arraycopy(pcmData, 0, wav, pos, dataSize)

        return wav
    }

    private fun writeString(buffer: ByteArray, offset: Int, value: String) {
        value.forEachIndexed { i, c -> buffer[offset + i] = c.code.toByte() }
    }

    private fun writeInt32(buffer: ByteArray, offset: Int, value: Int) {
        buffer[offset + 0] = (value and 0xFF).toByte()
        buffer[offset + 1] = ((value shr 8) and 0xFF).toByte()
        buffer[offset + 2] = ((value shr 16) and 0xFF).toByte()
        buffer[offset + 3] = ((value shr 24) and 0xFF).toByte()
    }

    private fun writeInt16(buffer: ByteArray, offset: Int, value: Short) {
        buffer[offset + 0] = (value.toInt() and 0xFF).toByte()
        buffer[offset + 1] = ((value.toInt() shr 8) and 0xFF).toByte()
    }
}
