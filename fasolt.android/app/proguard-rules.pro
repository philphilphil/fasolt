# Add project-specific ProGuard rules here.

# kotlinx.serialization — keep @Serializable classes
-keepattributes *Annotation*, InnerClasses
-dontnote kotlinx.serialization.AnnotationsKt

-keep,includedescriptorclasses class com.fasolt.android.**$$serializer { *; }
-keepclassmembers class com.fasolt.android.** {
    *** Companion;
}
-keepclasseswithmembers class com.fasolt.android.** {
    kotlinx.serialization.KSerializer serializer(...);
}

# Retrofit
-keepattributes Signature, Exceptions
-keep class retrofit2.** { *; }
