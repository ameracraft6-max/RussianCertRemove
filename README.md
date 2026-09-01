# 🛡️ Russian CA Remover

<p align="center">
  <b>Утилита для полного поиска и безопасного удаления сертификатов Минцифры («Russian Trusted Root CA», «Russian Trusted Sub CA») из Windows и всех браузеров с наглядным отчётом.</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-blue?style=flat-square&logo=dotnet" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows" alt="Windows 10/11" />
  <img src="https://img.shields.io/badge/Architecture-x64-brightgreen?style=flat-square" alt="x64" />
  <img src="https://img.shields.io/badge/License-MIT-green?style=flat-square" alt="MIT License" />
  <img src="https://img.shields.io/badge/Status-Stable-success?style=flat-square" alt="Stable" />
</p>

---

## 🎯 Назначение

При установке сертификатов Национального удостоверяющего центра (НУЦ) Минцифры РФ в системные хранилища они получают статус доверенных корневых центров. Это технически позволяет контролирующему органу осуществлять перехват и дешифровку защищённого HTTPS-трафика (MITM).

**Russian CA Remover** позволяет пользователю в один клик:
1. Найти все сертификаты Минцифры на компьютере (во всех ветках Windows и профилях браузеров).
2. Безопасно удалить их из системы.
3. Получить интерактивный и красивый HTML-отчёт о проделанной работе.

---

## ✨ Основные возможности

- 🔍 **Умное распознавание**:
  - Точное сопоставление по известным отпечаткам SHA-1 (`8FF915CCAB7BC16F8C5C8099D53E0E115B3AEC2F` и др.).
  - Поиск сертификатов `Russian Trusted Root CA`, `Russian Trusted Sub CA`, НУЦ, Госуслуги, ГНИВЦ и Минцифры.
  - Исключение ложных срабатываний на доверенные международные центры (Google, Microsoft, DigiCert).
- 🗄️ **Полная очистка хранилищ Windows CryptoAPI**:
  - `CurrentUser`: Доверенные корневые (`Root`), Промежуточные (`CA`), Личные (`My`), Сторонние (`AuthRoot`).
  - `LocalMachine`: Все системные разделы (с автоматической проверкой UAC и кнопкой запуска от Администратора).
- 🌐 **Поддержка всех браузеров**:
  - **Chromium-браузеры** (Google Chrome, Microsoft Edge, Яндекс Браузер, Opera, Brave, Vivaldi): определение активных процессов и уведомление о необходимости перезапуска для сброса TLS-сессий.
  - **Mozilla-браузеры** (Firefox, LibreWolf, Waterfox, Floorp, Thunderbird): прямое чтение и очистка базы `cert9.db` через встроенный `winsqlite3.dll` с автоматическим созданием резервной копии (`cert9.db.bak`).
- 🎨 **Современный Dark Fluent UI**:
  - Тёмная тема с неоновыми индикаторами статуса и карточками метрик.
  - Фильтрация и поиск по имени или отпечатку.
  - Модальное окно «Инфо» с полными реквизитами сертификата X.509.
  - Выборочное или пакетное удаление.
- 📄 **Красивые отчёты**:
  - Встроенный интерактивный веб-отчёт HTML с градиентами, таблицами и рекомендациями.
  - Экспорт в `.html`, `.txt` и копирование в буфер обмена.
- 🚀 **Работа «из коробки»**:
  - Доступен как автономный установщик (Setup Wizard), так и портативный исполняемый файл.

---

## 📥 Скачивание и установка

Скачайте последнюю версию со страницы **[Releases](https://github.com/ameracraft6-max/RussianCertRemove/releases)**:

| Вариант | Файл | Описание |
|---|---|---|
| **Установщик (Рекомендуется)** | `RussianCertRemover_Setup.exe` | Автономный мастер установки. Работает на любом ПК **без необходимости установки .NET**. Создает ярлыки на рабочем столе и в меню «Пуск». |
| **Портативная версия** | `RussianCertRemover.exe` | Быстрый запуск без установки (требуется установленный .NET 8 Desktop Runtime). |

---

## 🛠️ Сборка из исходного кода

### Требования
- ОС: Windows 10 / 11 (x64)
- .NET 8.0 SDK (Desktop Workload)
- Inno Setup 6 (для сборки инсталлятора)

### Сборка приложения
```bash
# Клонирование репозитория
git clone https://github.com/ameracraft6-max/RussianCertRemove.git
cd RussianCertRemove

# Сборка проекта
dotnet build RussianCertRemover/RussianCertRemover.csproj -c Release

# Публикация файлов приложения
dotnet publish RussianCertRemover/RussianCertRemover.csproj -c Release
```

### Сборка установщика
```bash
# Компиляция Inno Setup
iscc RussianCertRemover.iss
```

---

## 📄 Лицензия

Проект распространяется под свободной лицензией **MIT**. Подробности в файле [LICENSE](LICENSE).
