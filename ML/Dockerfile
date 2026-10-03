FROM python:3.11-slim

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends libgomp1 && rm -rf /var/lib/apt/lists/*

COPY ML/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

COPY ML/ ./ML/

RUN python3 ML/training/train_models.py

EXPOSE 8000

CMD ["uvicorn", "ML.api.main:app", "--host", "0.0.0.0", "--port", "8000"]
