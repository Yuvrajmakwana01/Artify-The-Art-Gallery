# 🎨 Artify - The Art Gallery

Artify is a full-stack web application that allows users to explore, review, and interact with digital artwork.

---

## 🚀 Tech Stack

* **Frontend:** ASP.NET MVC
* **Backend:** ASP.NET Core Web API
* **Database:** PostgreSQL
* **Caching:** Redis
* **Authentication:** JWT
* **Message Queue:** RabbitMQ
* **Logging & Monitoring:** ELK Stack (Elasticsearch, Logstash, Kibana)
* **Email Service:** SMTP-based Mail Service

---

## 📁 Project Structure

```
Artify/
│
├── API/            # Backend APIs (JWT, Redis, RabbitMQ, Mail Service)
├── MVC/            # Frontend UI
├── Repository/     # Data access layer
├── Artify.sln
```

---

## ⚙️ Setup Instructions

### 1. Clone the repository

```
git clone https://github.com/YOUR_USERNAME/Artify-The-Art-Gallery.git
cd Artify-The-Art-Gallery
```

---

### 2. Configure appsettings

#### API/appsettings.json

```
{
  "ConnectionStrings": {
    "pgconn": "YOUR_DB_CONNECTION",
    "Redis": "YOUR_REDIS_CONNECTION"
  },
  "Jwt": {
    "Key": "YOUR_SECRET_KEY",
    "Issuer": "your-app",
    "Audience": "your-users"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "guest",
    "Password": "guest"
  },
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "Port": 587,
    "SenderEmail": "your-email@gmail.com",
    "SenderPassword": "your-password"
  }
}
```

---

#### MVC/appsettings.json

```
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5001/api/"
  }
}
```

---

### 3. Run Required Services

Make sure the following services are running:

* PostgreSQL
* Redis
* RabbitMQ
* ELK Stack (Elasticsearch + Kibana)

---

### 4. Run the project

* Run **API project**
* Run **MVC project**

---

## 🔐 Features

* User Authentication (JWT)
* Redis Caching
* Asynchronous Processing using RabbitMQ
* Email Notifications (OTP / Alerts)
* Centralized Logging with ELK Stack
* Art Upload & Viewing
* Clean Architecture (Controller → Service → Repository)

---

## ⚡ System Architecture

* MVC interacts with API via HTTP
* API handles business logic
* RabbitMQ manages background tasks (emails, notifications)
* Redis improves performance via caching
* ELK Stack handles logging and monitoring

---

## 🤝 Contributors

* Yuvraj Makwana

---

## 📌 Future Improvements

* Role-based authentication
* Image upload optimization
* Real-time notifications
* Microservices architecture

---

## ⭐ Support

If you like this project, give it a ⭐ on GitHub!
