"""
NEXUS — Machine Learning Training, Validation, Evaluation & Serialization Pipeline
Trains and evaluates scikit-learn & XGBoost models across all 6 NEXUS prediction targets:
1. Demand
2. Supplier Failure
3. Inventory Shortage
4. Transportation Delay
5. Recovery Time
6. SLA Breach
"""
import json
import os
from pathlib import Path
import numpy as np
import pandas as pd
import joblib
from sklearn.model_selection import train_test_split
from sklearn.metrics import r2_score, mean_absolute_error, mean_squared_error, roc_auc_score, f1_score
from sklearn.ensemble import GradientBoostingRegressor, RandomForestClassifier

try:
    from xgboost import XGBRegressor
    HAS_XGB = True
except ImportError:
    HAS_XGB = False


BASE_DIR = Path(__file__).resolve().parent.parent
DATASETS_DIR = BASE_DIR / "datasets"
MODELS_DIR = BASE_DIR / "models"


def generate_telemetry_dataframe(n_samples: int = 3000, seed: int = 20261003) -> pd.DataFrame:
    rng = np.random.default_rng(seed)
    utilization = rng.uniform(55.0, 98.0, n_samples)
    supplier_reliability = rng.uniform(76.0, 99.5, n_samples)
    inventory_coverage = rng.uniform(4.0, 30.0, n_samples)
    lead_time_days = rng.uniform(2.0, 18.0, n_samples)
    demand_growth_pct = rng.uniform(-15.0, 45.0, n_samples)

    unreliability = (100.0 - supplier_reliability) / 100.0
    util_norm = utilization / 100.0
    cov_norm = inventory_coverage / 30.0

    demand = (
        2800.0
        + 950.0 * (demand_growth_pct / 100.0)
        + 420.0 * util_norm
        + rng.normal(0.0, 55.0, n_samples)
    )
    supplier_failure_prob = np.clip(
        unreliability * 310.0 + util_norm * 24.0 + (lead_time_days / 18.0) * 14.0 + rng.normal(0.0, 2.5, n_samples),
        1.0,
        99.0,
    )
    inventory_shortage_days = np.clip(
        inventory_coverage * (1.0 - 0.35 * (demand_growth_pct / 100.0)) * (supplier_reliability / 100.0)
        + rng.normal(0.0, 0.8, n_samples),
        1.0,
        35.0,
    )
    transport_delay_days = np.clip(
        0.3 + unreliability * 12.0 + util_norm * 1.8 + (lead_time_days * 0.12) + rng.normal(0.0, 0.22, n_samples),
        0.1,
        14.0,
    )
    recovery_time_days = np.clip(
        4.0 + unreliability * 28.0 + (1.0 - cov_norm) * 7.5 + util_norm * 4.5 + rng.normal(0.0, 0.65, n_samples),
        1.5,
        30.0,
    )
    sla_breach_prob = np.clip(
        unreliability * 145.0 + np.maximum(0.0, util_norm - 0.80) * 160.0 + (1.0 - cov_norm) * 22.0 + rng.normal(0.0, 1.4, n_samples),
        0.2,
        85.0,
    )

    return pd.DataFrame({
        "utilization_pct": np.round(utilization, 2),
        "supplier_reliability_pct": np.round(supplier_reliability, 2),
        "inventory_coverage_days": np.round(inventory_coverage, 2),
        "lead_time_days": np.round(lead_time_days, 2),
        "demand_growth_pct": np.round(demand_growth_pct, 2),
        "Demand": np.round(demand, 2),
        "Supplier Failure": np.round(supplier_failure_prob, 2),
        "Inventory Shortage": np.round(inventory_shortage_days, 2),
        "Transportation Delay": np.round(transport_delay_days, 2),
        "Recovery Time": np.round(recovery_time_days, 2),
        "SLA Breach": np.round(sla_breach_prob, 2),
    })


def train_and_serialize_all() -> dict:
    DATASETS_DIR.mkdir(parents=True, exist_ok=True)
    MODELS_DIR.mkdir(parents=True, exist_ok=True)

    df = generate_telemetry_dataframe(n_samples=3000)
    df.head(300).to_csv(DATASETS_DIR / "nexus_operational_telemetry.csv", index=False)

    feature_cols = [
        "utilization_pct",
        "supplier_reliability_pct",
        "inventory_coverage_days",
        "lead_time_days",
        "demand_growth_pct",
    ]
    targets = [
        "Demand",
        "Supplier Failure",
        "Inventory Shortage",
        "Transportation Delay",
        "Recovery Time",
        "SLA Breach",
    ]

    X = df[feature_cols].values
    metrics_report = {}

    for target in targets:
        y = df[target].values
        X_train, X_val, y_train, y_val = train_test_split(X, y, test_size=0.20, random_state=42)

        if HAS_XGB:
            reg = XGBRegressor(n_estimators=120, max_depth=4, learning_rate=0.07, random_state=42)
            algo_name = "XGBoostRegressor (n_estimators=120, max_depth=4)"
        else:
            reg = GradientBoostingRegressor(n_estimators=120, max_depth=4, learning_rate=0.07, random_state=42)
            algo_name = "GradientBoostingRegressor (n_estimators=120, max_depth=4)"

        reg.fit(X_train, y_train)
        y_pred = reg.predict(X_val)

        r2 = float(r2_score(y_val, y_pred))
        mae = float(mean_absolute_error(y_val, y_pred))
        rmse = float(np.sqrt(mean_squared_error(y_val, y_pred)))

        median_thresh = float(np.median(y_val))
        y_bin_true = (y_val >= median_thresh).astype(int)
        y_bin_pred = (y_pred >= median_thresh).astype(int)
        roc_auc = float(roc_auc_score(y_bin_true, y_pred))
        f1 = float(f1_score(y_bin_true, y_bin_pred))

        importances = getattr(reg, "feature_importances_", np.ones(len(feature_cols)) / len(feature_cols))
        feat_imp = {col: round(float(val), 4) for col, val in zip(feature_cols, importances)}

        slug = target.lower().replace(" ", "_")
        joblib.dump(reg, MODELS_DIR / f"{slug}_model.joblib")

        metrics_report[target] = {
            "target_domain": target,
            "algorithm": algo_name,
            "training_samples": int(len(X_train)),
            "validation_samples": int(len(X_val)),
            "r2_score": round(r2, 4),
            "mae": round(mae, 3),
            "rmse": round(rmse, 3),
            "roc_auc": round(roc_auc, 4),
            "f1_score": round(f1, 4),
            "feature_importances": feat_imp,
        }

    with open(MODELS_DIR / "evaluation_metrics.json", "w", encoding="utf-8") as f:
        json.dump(metrics_report, f, indent=2)

    return metrics_report


if __name__ == "__main__":
    report = train_and_serialize_all()
    print(f"Trained & serialized {len(report)} NEXUS ML models successfully.")
