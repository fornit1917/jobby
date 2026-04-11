import { defineConfig } from 'vitepress'

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: "Jobby",
  description: "High-performance and reliable .NET library for background jobs",
  base: "/jobby/",
  themeConfig: {
    // https://vitepress.dev/reference/default-theme-config
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Docs', link: '/docs/what-is-jobby', activeMatch: '^/docs/' }
    ],

    sidebar: [
      {
        text: 'Overview',
        items: [
          { text: 'What is Jobby', link: '/docs/what-is-jobby' },
          { text: 'Quick start', link: '/docs/quickstart' },
        ]
      },
      {
        text: 'Usage Guide',
        items: [
          { text: 'Install and Config', link: '/docs/install-and-config' },
          { text: 'Jobs definition', link: '/docs/jobs-definition' },
          { text: 'Jobs running', link: '/docs/jobs-enqueue' },
          { text: 'Scheduled jobs', link: '/docs/jobs-schedule' },
          { text: 'Retry policies', link: '/docs/retry-policies' },      
          { text: 'Middlewares', link: '/docs/middlewares' },
          { text: 'Metrics and tracing', link: '/docs/observability' },
          { text: 'Fault tolerance', link: '/docs/fault-tolerance' },
          { text: 'Multi-queues', link: '/docs/multiqueues' },
          { text: 'Sequential execution', link: '/docs/sequential-execution' },
        ]
      },
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/fornit1917/jobby' }
    ]
  },

  locales: {
    root: {
      label: 'English',
      lang: 'en'
    },
    ru: {
      label: 'Русский',
      lang: 'ru',
      dir: 'ru',
      description: 'Высокопроизводительная и надёжная .NET библиотека для фоновых задач',
      themeConfig: {
        nav: [
          { text: 'Главная', link: '/ru', activeMatch: '^/ru/$' },
          { text: 'Документация', link: '/ru/docs/what-is-jobby', activeMatch: '^/ru/docs/' }
        ],

        sidebar: [
          {
            text: 'Обзор Jobby',
            items: [
              { text: 'Что такое Jobby', link: '/ru/docs/what-is-jobby' },
              { text: 'Быстрый старт', link: '/ru/docs/quickstart' },
            ]
          },
          {
            text: 'Инструкция',
            items: [
              { text: 'Установка и настройка', link: '/ru/docs/install-and-config' },
              { text: 'Описание задач', link: '/ru/docs/jobs-definition' },
              { text: 'Запуск задач', link: '/ru/docs/jobs-enqueue' },
              { text: 'Задачи по расписанию', link: '/ru/docs/jobs-schedule' },
              { text: 'Настройка повторов', link: '/ru/docs/retry-policies' },
              { text: 'Middlewares', link: '/ru/docs/middlewares' },
              { text: 'Метрики и трейсинг', link: '/ru/docs/observability' },
              { text: 'Устойчивость к сбоям', link: '/ru/docs/fault-tolerance' },
              { text: 'Мульти-очереди', link: '/ru/docs/multiqueues' },
              { text: 'Последовательное выполнение', link: '/ru/docs/sequential-execution' },                                     
            ]
          },
        ],
      }
    }
  }
})
