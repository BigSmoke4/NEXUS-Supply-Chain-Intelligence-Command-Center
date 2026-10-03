"""
NEXUS FastAPI Machine Learning Inference Service
Exposes /health, /metrics, /predict, and /train for ASP.NET Core integration.
"""
import json
from pathlib import Path
from fastapi import FastAPI
from pydantic import BaseModel
import joblib
import numpy as np

from ML.training.train_models import train_and_serialize_all

BASE_DIR = Path(__file__).resolve().parent.parent
MODELS_DIR = BASE_DIR / "models"

app = FastAPI(
    title="NEXUS Enterprise ML Inference Service",
    version="2.1.0",
    description="Scikit-Learn & XGBoost predictive inference API for NEXUS Autonomous Enterprise Decision Engine",
)


class PredictionRequest(BaseModel):
    target_domain: str = "Demand"
    target_asset_code: str = "SUP-001"
    horizon_days: int = 14
    utilization_pct: float = 87.0
    supplier_reliability_pct: float = 91.0
    inventory_coverage_days: float = 19.0
    lead_time_days: float = 6.0
    demand_growth_pct: float = 8.0


@app.on_event("startup")
def ensure_models_ready():
    metrics_file = MODELS_DIR / "evaluation_metrics.json"
    if not metrics_file.exists():
        train_and_serialize_all()


@app.get("/health")
def health_check():
    return {"status": "healthy", "service": "nexus-ml-api", "engine": "scikit-learn + xgboost"}


@app.get("/metrics")
def get_evaluation_metrics():
    metrics_file = MODELS_DIR / "evaluation_metrics.json"
    if not metrics_file.exists():
        return train_and_serialize_all()
    with open(metrics_file, "r", encoding="utf-8") as f:
        return json.load(f)


@app.post("/train")
def retrain_models():
    report = train_and_serialize_all()
    return {"status": "completed", "models_trained": len(report), "metrics": report}


@app.post("/predict")
def predict(req: PredictionRequest):
    slug = req.target_domain.lower().replace(" ", "_")
    model_path = MODELS_DIR / f"{slug}_model.joblib"
    if not model_path.exists():
        train_and_serialize_all()
    if not model_path.exists():
        model_path = MODELS_DIR / "demand_model.joblib"

    model = joblib.load(model_path)
    X = np.array([[
        req.utilization_pct,
        req.supplier_reliability_pct,
        req.inventory_coverage_days,
        req.lead_time_days,
        req.demand_growth_pct,
    ]])
    pred_val = float(model.predict(X)[0])
    prob_pct = float(np.clip((100.0 - req.supplier_reliability_pct) * 3.5 + (req.utilization_pct * 0.45), 3.0, 98.5))

    return {
        "targetDomain": req.target_domain,
        "targetAssetCode": req.target_asset_code,
        "predictedValue": round(pred_val, 3),
        "probabilityPct": round(prob_pct, 2),
    }
