# SmartEnterpriseInbox - AI-Powered Autonomous Email Routing Engine

SmartEnterpriseInbox, kurumsal e-posta kutularına gelen müşteri ve personel taleplerini yapay zeka ile anlık olarak analiz eden, sınıflandıran, özetleyen ve ilgili departmanlara (İK, Muhasebe, Bilgi İşlem vb.) otonom olarak yönlendiren **Enterprise-Grade (Kurumsal Seviye)** bir arka plan iş akışı motorudur.

Proje; yüksek trafik altında veri kaybını önleyen mimari pattern'ler, maliyet düşüren anlamsal önbellekleme mekanizmaları ve gevşek bağlı (loosely coupled) mikroservis yaklaşımları esas alınarak geliştirilmiştir.

---

## 🏗️ Mimari Yapı ve Patternler (Architectural Patterns)

Proje, kurumsal yazılım standartlarına tam uyum sağlamak adına aşağıdaki mimari yaklaşımlarla tasarlanmıştır:

* **Clean Architecture (Temiz Mimari):** İş kuralları (Core) ve teknolojik bağımlılıklar (Infrastructure/WebApi) tamamen birbirinden soyutlanmıştır. Veritabanı veya mesaj kuyruğu değişse bile çekirdek iş mantığı bundan etkilenmez.
* **Transactional Outbox Pattern:** Yapay zeka analizi bittiğinde, veri tabanına (PostgreSQL) kayıt atılması ile RabbitMQ'ya mesaj fırlatılması işlemleri **tek bir veritabanı transaction'ı** içinde atomik olarak yürütülür. Bu sayede ağ kopmalarında mesaj kaybı (Message Loss) ihtimali sıfıra indirilir.
* **Semantic Caching (Anlamsal Önbellekleme):** Gelen mailler doğrudan LLM'e gönderilmez. Önce mail içeriğinin vektörü üretilir ve Redis üzerinde Kosinüs Benzerliği ($Cosine\ Similarity$) hesaplanarak daha önce benzer bir mail gelip gelmediği kontrol edilir. Eğer anlamsal benzerlik $\%85$ ve üzeriyse, yanıt doğrudan Redis'ten dönülerek API maliyeti ve gecikme süresi (latency) düşürülür.
* **CQRS & Background Tasks:** Yoğun iş yükü oluşturan e-posta dinleme, kuyruk yönetimi ve SMTP yönlendirme işlemleri ana API'yi kilitlememesi için bağımsız `.NET BackgroundService` (Worker) yapıları üzerinden asenkron yürütülür.

---

## 🛠️ Kullanılan Teknolojiler ve Kütüphaneler

* **Backend Framework:** .NET 8
* **AI Orchestration:** Microsoft Semantic Kernel & Google AI Studio (Gemini REST API)
* **AI Model (Embedding):** `gemini-embedding-001` (v1/models üzerinden saf REST entegrasyonu)
* **Database & ORM:** PostgreSQL & Entity Framework Core
* **Caching & Vector Storage:** Redis (StackExchange.Redis)
* **Message Broker:** RabbitMQ
* **Email Protocols (IMAP/SMTP):** MailKit

---

## 🔄 Uçtan Uca İş Akışı (Data Flow)

1.  **Email Ingestion (Gmail Entegrasyonu):** `EmailReceiverBackgroundService` 30 saniyelik periyotlarla Gmail kutusunu IMAP üzerinden dinler ve sadece okunmamış (Unread) mailleri yakalar.
2.  **Semantic Cache Check:** Yakalanan mail içeriği `SemanticCacheService`'e gönderilir. Mailin embedding vektörü çıkarılarak Redis'teki eski kayıtlarla kıyaslanır.
    * *Cache Hit:* Yakın bir talep varsa doğrudan eski analiz sonucu alınır.
    * *Cache Miss:* Mail, Semantic Kernel aracılığıyla Gemini modeline yönlendirilir ve JSON formatında yapılandırılmış analiz (Kategori, Aciliyet, Özet, Aksiyon Planı) üretilir.
3.  **Atomic Persistence & Outbox:** Üretilen talep ve kuyruğa atılacak event emri, veritabanına tek bir transaction ile mühürlenir. İşlenen mail Gmail üzerinde "Okundu" olarak işaretlenir.
4.  **Outbox Publishing:** `OutboxPublisherBackgroundService`, veri tabanındaki işlenmemiş outbox kayıtlarını periyodik olarak tarar ve mesajları güvenli bir şekilde RabbitMQ'nun `email_routing_queue` kuyruğuna push eder.
5.  **Routing & Notification:** `EmailRoutingBackgroundService` (Consumer), RabbitMQ'dan mesajı yakalar. Yapay zekanın belirlediği kategoriye göre (İK, Muhasebe vb.) `appsettings.json` üzerindeki ilgili departman mailini çözer ve zengin bir HTML şablonla SMTP üzerinden hedef departmana yönlendirir.

---

## ⚙️ Kurulum ve Yapılandırma

Projeyi ayağa kaldırmadan önce;

1- Docker klasörü içindeki docker-compose.yml dosyasını docker compose up -d komutu ile çalıştırın ve içinde yer alan postgres, rabbitmq, redis gibi araçları kurun.

2- Aşağıdaki komutlar ile db üzerinde tabloları oluşturun.
```
  Add-Migration AddOutboxPattern -StartupProject SmartEnterpriseInbox.WebApi -Project SmartEnterpriseInbox.Infrastructure -OutputDir Migrations
  Update-Database -StartupProject SmartEnterpriseInbox.WebApi -Project SmartEnterpriseInbox.Infrastructure
```
### appsettings.json Konfigürasyonu

3- `appsettings.json` dosyasındaki aşağıdaki alanları kendi kurumsal veya kişisel hesaplarınıza göre doldurmanız gerekmektedir:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=enterprise_inbox_db;Username=enterprise_user;Password=your_password"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY"
  },
  "EmailSettings": {
    "ImapServer": "imap.gmail.com",
    "ImapPort": 993,
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "senin_adresin@gmail.com",
    "AppPassword": "xxxx xxxx xxxx xxxx", 
    "Departments": {
      "InsanKaynaklari": "ik_departmani@gmail.com",
      "Muhasebe": "muhasebe_departmani@gmail.com",
      "TeknikDestek": "destek_departmani@gmail.com",
      "Belirsiz": "genel_yonetim@gmail.com"
    }
  }
}
