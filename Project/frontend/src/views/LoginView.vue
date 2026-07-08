<template>
  <div class="min-h-screen flex items-center justify-center bg-base-200">
    <div class="card w-full max-w-md shadow-xl bg-base-100">
      <div class="card-body">
        <h1 class="card-title text-2xl justify-center">
          {{ $t('auth.login') }}
        </h1>
        <div class="form-control mt-4">
          <label class="label">
            <span class="label-text">{{ $t('auth.email') }}</span>
          </label>
          <input
            type="email"
            class="input input-bordered"
            :placeholder="$t('auth.email')"
          >
        </div>
        <div class="form-control mt-2">
          <label class="label">
            <span class="label-text">{{ $t('auth.password') }}</span>
          </label>
          <input
            type="password"
            class="input input-bordered"
            :placeholder="$t('auth.password')"
          >
        </div>
        <div class="card-actions justify-end mt-6">
          <button class="btn btn-primary w-full">
            {{ $t('auth.loginButton') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
// LoginView — public route, no auth required
// Stub: no real auth logic yet (implemented in auth change)
</script>
