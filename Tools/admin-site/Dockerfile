# 어드민 사이트 컨테이너 이미지
# 빌드: docker build -t todays-weapon-rental-admin .
# 실행: docker run -p 8000:8000 --env-file .env todays-weapon-rental-admin
FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app

EXPOSE 8000

# PORT 환경변수가 있으면 사용 (Fly.io/Cloud Run 호환)
CMD ["sh", "-c", "uvicorn app.main:app --host 0.0.0.0 --port ${PORT:-8000}"]
