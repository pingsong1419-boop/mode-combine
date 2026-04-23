import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  base: './',
  plugins: [vue()],
  server: {
    port: 5173,
    host: '0.0.0.0',
    proxy: {
      // 将 /mes-api/* 代理到 MES 服务器，解决浏览器 CORS 问题
      '/mes-api': {
        target: 'http://127.0.0.1:8076',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/mes-api/, ''),
        configure: (proxy) => {
          proxy.on('error', (err) => {
            console.log('[MES代理错误]', err.message)
          })
          proxy.on('proxyReq', (_, req) => {
            console.log('[MES代理请求]', req.method, req.url)
          })
        }
      }
    }
  },
  build: {
    // 构建优化
    target: 'es2015',
    minify: 'esbuild'
  }
})
