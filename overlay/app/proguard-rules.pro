# Add project specific ProGuard rules here.
# By default, the flags in this file are appended to flags specified
# in the Android SDK tools proguard/proguard-android.txt

# Keep JavaScript interface methods for WebView bridge
-keepclassmembers class com.vibereal.overlay.VoiceBridge {
    @android.webkit.JavascriptInterface <methods>;
}
