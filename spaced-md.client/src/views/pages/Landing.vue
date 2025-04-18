<script setup lang="ts">
</script>

<template>
  <div class="min-h-screen flex flex-col justify-center items-center bg-gray-100 p-4">
    <div class="bg-white shadow-md rounded-lg p-8 w-full max-w-md">
      <img src="../../assets/images/logo_full.png" alt="Logo" class="h-50 mb-4 block mx-auto" />
      <p class="mb-4">
        Unlock your true potential with Spaced Repetition Markdown! Experience rapid learning and effortless retention
        as our advanced algorithm transforms every markdown file into a catalyst for your success. Revolutionize your
        note-taking and never miss a beat in your journey to brilliance!
      </p>
      <div class="flex space-x-4">
        <router-link to="/auth/signup" class="text-blue-500 hover:underline">
          Sign Up
        </router-link>
        <router-link to="/auth/login" class="text-blue-500 hover:underline ml-auto">
          Log In
        </router-link>
      </div>
    </div>
  </div>
</template>
