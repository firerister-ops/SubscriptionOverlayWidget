# SubscriptionOverlayWidget

Небольшой WPF-виджет для Windows: сидит в системном трее и умеет показывать оверлей с лимитами подписки поверх всех окон.

## Что умеет

- tray icon + контекстное меню
- overlay в правом верхнем углу
- периодический запрос `GET https://ai.wormsoft.ru/api/gpt/subscription-limit`
- заголовок `Authorization: Bearer <token>`
- окно настроек для API key, интервала, цветов, размеров и позиции оверлея
- кнопка `Move overlay` для ручного перетаскивания окна
- компактный click-through overlay поверх всех окон
- эвристический таймер до сброса по типу подписки и последнему замеченному reset/increase

## Запуск

```powershell
dotnet build .\SubscriptionOverlayWidget\SubscriptionOverlayWidget.csproj
dotnet run --project .\SubscriptionOverlayWidget\SubscriptionOverlayWidget.csproj

# single-file exe
dotnet publish .\SubscriptionOverlayWidget\SubscriptionOverlayWidget.csproj -c Release -r win-x64
```

## Настройки

Файл настроек хранится здесь:

`%AppData%\SubscriptionOverlayWidget\settings.json`

Пример:

```json
{
  "ApiKey": "your_api_key_here",
  "RefreshIntervalSeconds": 60,
  "StartOverlayVisible": true,
  "OverlayBackgroundColor": "#CC111111",
  "OverlayTextColor": "#FFFFFFFF",
  "OverlayBorderColor": "#44FFFFFF",
  "OverlayFontSize": 14,
  "OverlayWidth": 340,
  "OverlayHeight": 220,
  "OverlayLeft": -1,
  "OverlayTop": -1
}
```

Вводите только API key, без слова `Bearer`.

## Меню в трее

- Show Overlay
- Hide Overlay
- Settings
- Refresh Now
- Exit

## Примечание по API

Сейчас виджет ожидает такой ответ:

```json
{
  "subcriptionType": "free",
  "subcriptionLimit": 2458957
}
```

И показывает его как:

- `Подписка: free`
- `Осталось: 2458957`

## Эвристика таймера

В API нет явного `resetAt`, поэтому таймер считается эвристически:

- `free` — каждые 10 часов
- `promo` — каждые 8 часов
- `simple` — каждые 5 часов
- `payed` — каждые 4 часа
- `wormsoft developer` — каждые 2 часа
- `wormsoft boss` — каждый час

Последний сброс считается произошедшим в момент:
- первого успешного наблюдения тарифа,
- смены тарифа,
- или когда остаток лимита вырос относительно предыдущего значения.
