import streamlit as st
import pandas as pd
import plotly.express as px
import joblib
import os
import data_cleaner
import stopword_remover
import model_trainer

st.set_page_config(page_title="MoodMath NLP", page_icon="🧠", layout="wide")

st.sidebar.title("🧠 MoodMath Paneli")
st.sidebar.markdown("---")
menu = st.sidebar.radio("İşlem Seçiniz:",
                        ("1. Veri Boru Hattı & Metrikler",
                         "2. Model Eğitimi & Ayarlar",
                         "3. Canlı Test & Şeffaf Analiz"))

# --- 1. SAYFA: VERİ GÖRSELLEŞTİRME VE TEMİZLİK ---
if menu == "1. Veri Boru Hattı & Metrikler":
    st.title("📊 Veri Ön İşleme ve Metrikler")
    st.write("Ham verilerinizi NLP'ye hazırlayın ve genel tabloya göz atın.")

    col1, col2 = st.columns(2)
    with col1:
        st.subheader("Adım 1: Temizlik (Regex)")
        if st.button("Ham Veriyi Temizle", use_container_width=True):
            with st.spinner("Temizleniyor..."):
                data_cleaner.run_cleaner()
            st.success("Temizlik Tamam!")

    with col2:
        st.subheader("Adım 2: NLTK Stopwords")
        if st.button("Etkisiz Kelimeleri Ayıkla", use_container_width=True):
            with st.spinner("Ayıklanıyor..."):
                stopword_remover.run_nltk_cleaning()
            st.success("NLP'ye Hazır!")

    st.markdown("---")
    st.subheader("📌 Güncel Veri Seti Özeti")
    try:
        df = pd.read_csv("CSV/cleaned_reviews.csv")

        # Metrikler (Dashboard)
        total_reviews = len(df)
        pos_count = len(df[df['Sentiment'] == 1])
        neg_count = len(df[df['Sentiment'] == 0])

        c1, c2, c3 = st.columns(3)
        c1.metric("Toplam Yorum", f"{total_reviews:,}")
        c2.metric("Pozitif Yorum", f"{pos_count:,}", f"{(pos_count / total_reviews) * 100:.1f}%")
        c3.metric("Negatif Yorum", f"{neg_count:,}", f"-{(neg_count / total_reviews) * 100:.1f}%")

        # Görselleştirme
        st.write("**Duygu Dağılım Grafiği**")
        chart_data = pd.DataFrame({"Yorum Sayısı": [neg_count, pos_count]}, index=["Negatif", "Pozitif"])
        # --- GÜNCEL VE ŞIK GRAFİK (PLOTLY) ---
        st.markdown("---")
        st.write("**Duygu Dağılım Grafiği**")

        # Grafik için veriyi hazırlıyoruz
        df_chart = pd.DataFrame({
            "Duygu": ["Negatif", "Pozitif"],
            "Yorum Sayısı": [neg_count, pos_count]
        })

        # Plotly Express ile grafiği çiziyoruz
        fig = px.bar(
            df_chart,
            x="Duygu",
            y="Yorum Sayısı",
            color="Duygu",
            color_discrete_map={"Negatif": "#ff4b4b", "Pozitif": "#21c354"},  # Özel renkler
            text="Yorum Sayısı"  # Sütunların üstüne rakamları yaz
        )

        # Tasarım İnce Ayarları
        fig.update_traces(
            width=0.3,  # Sütun kalınlığı (0.3 çok daha ince ve zarif durur)
            textposition='outside'  # Rakamları sütunun üstüne koy
        )
        fig.update_layout(
            xaxis_tickangle=0,  # X eksenindeki yazıları (Negatif/Pozitif) tam yatay yap
            showlegend=False,  # Yanda gereksiz kutucuk (legend) çıkmasın
            margin=dict(t=30, b=10, l=10, r=10),  # Kenar boşluklarını daralt
            height=400  # Grafiğin genel boyu
        )

        # Grafiği Streamlit'e bas
        st.plotly_chart(fig, use_container_width=True)
    except:
        st.info("Metrikleri görmek için lütfen önce veriyi temizleyin.")

# --- 2. SAYFA: HİPERPARAMETRE KONTROL PANELİ ---
elif menu == "2. Model Eğitimi & Ayarlar":
    st.title("⚙️ Yapay Zeka Model Eğitimi")
    st.write("Modelin beynine ince ayar yapın. (Hiperparametre Optimizasyonu)")

    st.markdown("### 🎛️ Kontrol Paneli")
    colA, colB = st.columns(2)
    with colA:
        max_feat = st.slider("Kelime Dağarcığı (Max Features)", min_value=1000, max_value=10000, value=5000, step=500,
                             help="Modelin ezberleyeceği en önemli kelime sayısı.")
    with colB:
        test_ratio = st.slider("Test Verisi Oranı", min_value=0.1, max_value=0.5, value=0.2, step=0.05,
                               help="%20 seçilirse verinin %80'i eğitim, %20'si sınav için ayrılır.")

    if st.button("🚀 Modeli Bu Ayarlarla Eğit", type="primary"):
        with st.spinner("Makine ders çalışıyor..."):
            result = model_trainer.run_training(max_features=max_feat, test_size=test_ratio)

            if isinstance(result, tuple):
                accuracy, _ = result
                st.success("Eğitim Başarılı! Model hafızaya kaydedildi.")
                st.metric("Sınav Başarısı (Accuracy)", f"% {accuracy * 100:.2f}")
            else:
                st.error(result)

# --- 3. SAYFA: ŞEFFAF YAPAY ZEKA (AÇIKLANABİLİRLİK) ---
elif menu == "3. Canlı Test & Şeffaf Analiz":
    st.title("🕵️ Şeffaf Yapay Zeka (Explainable AI)")
    st.write("Model sadece pozitif/negatif demekle kalmaz, bu kararı *neden* verdiğini de açıklar.")

    # Model yüklü mü kontrolü
    if not os.path.exists("Model/mood_model.pkl"):
        st.warning("Lütfen önce 2. sayfaya gidip bir model eğitin!")
    else:
        model = joblib.load("Model/mood_model.pkl")
        vectorizer = joblib.load("Model/mood_vectorizer.pkl")

        user_input = st.text_area("Müşteri Yorumunu Yapıştırın:", height=100)

        if st.button("Analiz Et ve Açıkla"):
            if user_input:
                # Tahmin yap
                user_vec = vectorizer.transform([user_input])
                prediction = model.predict(user_vec)[0]
                prob = model.predict_proba(user_vec)[0]  # Emin olma yüzdesi

                # Kararı Göster
                if prediction == 1:
                    st.success(f"### Tahmin: POZİTİF 😊 (Eminlik: % {prob[1] * 100:.1f})")
                else:
                    st.error(f"### Tahmin: NEGATİF 😡 (Eminlik: % {prob[0] * 100:.1f})")

                st.markdown("---")
                st.subheader("🔍 Neden Bu Kararı Verdim?")
                st.write("Makine bu cümledeki kelimelere aşağıdaki puanları verdi:")

                # AÇIKLANABİLİRLİK MATEMATİĞİ (Feature Importance)
                feature_names = vectorizer.get_feature_names_out()
                coefficients = model.coef_[0]
                nonzero_indices = user_vec.nonzero()[1]

                word_weights = [(feature_names[idx], coefficients[idx]) for idx in nonzero_indices]

                col_pos, col_neg = st.columns(2)
                with col_pos:
                    st.write("👍 **Puanı Yükseltenler**")
                    pos_words = sorted([w for w in word_weights if w[1] > 0], key=lambda x: x[1], reverse=True)
                    for w, weight in pos_words:
                        st.write(f"- {w.capitalize()}: `+{weight:.2f}`")

                with col_neg:
                    st.write("👎 **Puanı Düşürenler**")
                    neg_words = sorted([w for w in word_weights if w[1] < 0], key=lambda x: x[1])
                    for w, weight in neg_words:
                        st.write(f"- {w.capitalize()}: `{weight:.2f}`")
            else:
                st.warning("Lütfen bir yorum yazın.")