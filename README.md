# JobbAgent

AI-agenter som hjälper dig att söka jobb. De letar upp annonser som passar ditt CV, visar hur väl du matchar
varje annons och skriver ett personligt brev som bara bygger på det som faktiskt står i ditt CV.

![Sökresultat](docs/sokning.png)

## Två sätt att använda den

**🔎 Hitta jobb åt mig.** Ange gärna ort, egna sökord och om du vill jobba på distans. Sökaren läser ditt CV,
tar fram sökord och söker i Arbetsförmedlingens Platsbanken. Matcharen ger varje annons en poäng (0–100 %)
och en kort motivering. Klicka på *Skriv brev* vid den annons du vill söka.

**✉️ Egen annons.** Klistra in en länk eller själva annonstexten. Platsbanken-länkar läses via
Arbetsförmedlingens API; andra sidor hämtas och görs om till text. Om sidan kräver inloggning (t.ex. LinkedIn)
klistrar du in texten i stället.

Brevet kan du redigera, spara, kopiera och ladda ner. Allt sparas i historiken.

![Personligt brev](docs/brev.png)

## Agenterna

| Agent | Gör | Lämnar över till |
|---|---|---|
| 🔎 **Sökare** | Tar fram 1–3 sökfraser ur CV:t och söker i Platsbanken (gratis API, ingen nyckel). | Matchare: upp till 15 annonser |
| 📄 **Tolkare** | Plockar ut krav, meriter, arbetsuppgifter, personliga egenskaper och kontaktperson ur annonsen. | Matchare: kraven |
| 🎯 **Matchare** | Rankar sökträffar, eller går igenom en annons krav för krav: vad som passar, vad som saknas och vad brevet ska lyfta fram. | Skribent: analysen |
| ✍️ **Skribent** | Skriver brevet (250–400 ord) på svenska eller engelska. Hittar inte på något som inte står i CV:t. | Granskare: brevet |
| ✅ **Granskare** | Kontrollerar brevet mot CV:t och annonsen: påhittade meriter, missade krav, fel språk, platshållare. | Skribent igen (högst en gång) |

```
Sökning:  CV → Sökare → Platsbanken → Matchare → Topplista ──┐
                                                             │ "Skriv brev"
Brev:     Annons → Tolkare → Matchare → Skribent → Granskare → Personligt brev
                                           ↑          │
                                           └──────────┘  (om brevet underkänns)
```

Orkestreraren (`backend/JobbAgent.Api/Services/RunOrchestrator.cs`) styr ordningen. Varje steg sparas i
SQL Server och skickas live till webbläsaren med Server-Sent Events.

## Teknik

- **Backend:** .NET 10 Minimal API, EF Core, SQL Server, bakgrundskö för körningar
- **AI:** Groq (`openai/gpt-oss-120b`) med structured outputs (JSON-schema genererat från C#-records)
- **Jobbannonser:** [JobSearch API](https://jobsearch.api.jobtechdev.se) från Arbetsförmedlingen/JobTech
- **CV:** PDF (PdfPig), Word (.docx) eller vanlig text
- **Frontend:** React 19 + Vite + TypeScript
- **Drift:** Docker Compose, GitHub Actions kör tester på varje pull request

## Kom igång (Windows)

1. Installera [Docker Desktop](https://www.docker.com/products/docker-desktop/) och hämta koden:
   `git clone https://github.com/Khalid1754/JobbAgent.API.git`
2. Dubbelklicka på `start.bat`. Första gången skapas `.env` och öppnas i Anteckningar:
   lägg in din `GROQ_API_KEY` (gratis på https://console.groq.com/keys), spara och kör `start.bat` igen.
3. Appen öppnas på http://localhost:8100. Börja med att ladda upp ditt CV under *Mitt CV*.
   `stopp.bat` stoppar appen.

Eller i PowerShell:

```powershell
copy .env.example .env   # och fyll i GROQ_API_KEY
docker compose up -d --build
```

Portarna (8100, 5108, 1435) skiljer sig från TrAInr och AgentFlow, så alla kan köras samtidigt.

## Utveckla lokalt

```powershell
docker compose up -d sqlserver
cd backend/JobbAgent.Api
dotnet user-secrets init; dotnet user-secrets set Groq:ApiKey <din-nyckel>
dotnet run                      # API på http://localhost:5108
cd ../../frontend
npm install; npm run dev        # http://localhost:5173
```

Tester: `dotnet test JobbAgent.slnx` och `npm test` i `frontend`.

## API

| Metod | Sökväg | Beskrivning |
|---|---|---|
| `GET` | `/api/profile` | Ditt sparade CV |
| `PUT` | `/api/profile` | Sparar CV-text, `{ "cvText": "..." }` |
| `POST` | `/api/profile/upload` | Laddar upp CV som fil (`file`, PDF/.docx/.txt, max 5 MB) |
| `POST` | `/api/searches` | Startar en jobbsökning, `{ "location": "Stockholm", "keywords": "", "remoteOnly": false }` |
| `POST` | `/api/letters` | Startar ett brev, `{ "adUrl": "..." }`, `{ "adText": "..." }` eller `{ "platsbankenId": "..." }`, plus `language`: `auto`, `sv` eller `en` |
| `GET` | `/api/runs` | Historiken (sökningar och brev) |
| `GET` | `/api/runs/{id}` | En körning med alla steg och resultat |
| `GET` | `/api/runs/{id}/events` | Server-Sent Events: alla steg, sedan nya live |
| `PUT` | `/api/runs/{id}/letter` | Sparar ändringar i brevet |
| `DELETE` | `/api/runs/{id}` | Tar bort en färdig körning |
| `GET` | `/api/status` | Om AI-nyckeln och CV:t är inlagda |

## Integritet

Ditt CV och dina brev sparas bara i din egen SQL Server-databas. CV:t och annonsen skickas till Groq när
agenterna jobbar.
