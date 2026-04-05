import streamlit as st
import pandas as pd
import plotly.express as px
import matplotlib.pyplot as plt
from wordcloud import WordCloud
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
                         "3. Toplu Analiz (JSON/CSV)",
                         "4. Canlı Test & Şeffaf Analiz"))

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
elif menu == "3. Toplu Analiz (JSON/CSV)":
    st.title("📂 Toplu Veri Analizi")
    st.write("C# Harvester ile topladığınız dosyaları yükleyin, saniyeler içinde analiz edelim.")

    uploaded_file = st.file_uploader("Bir JSON veya CSV dosyası seçin", type=['json', 'csv'])

    if uploaded_file is not None:
        # Dosyayı oku
        if uploaded_file.name.endswith('.json'):
            df_new = pd.read_json(uploaded_file)
        else:
            df_new = pd.read_csv(uploaded_file)

        st.write(f"Sistemde {len(df_new)} adet yeni yorum tespit edildi.")

        if st.button("Tümünü Analiz Et"):
            if not os.path.exists("Model/mood_model.pkl"):
                st.error("Önce model eğitmelisiniz!")
            else:
                model = joblib.load("Model/mood_model.pkl")
                vectorizer = joblib.load("Model/mood_vectorizer.pkl")

                # Tahmin yap (Yorum sütununun isminin 'Comment' olduğunu varsayıyoruz)
                with st.spinner("Yapay zeka binlerce satırı okuyor..."):
                    # Temizlik fonksiyonunu burada tekrar çağırabiliriz
                    df_new['Cleaned'] = df_new['Comment'].apply(lambda x: str(x).lower())
                    X_new = vectorizer.transform(df_new['Cleaned'])
                    df_new['Tahmin'] = model.predict(X_new)
                    df_new['Duygu'] = df_new['Tahmin'].map({1: "Pozitif 😊", 0: "Negatif 😡"})

                st.success("Analiz Tamamlandı!")
                st.dataframe(df_new[['Comment', 'Duygu']].head(10), use_container_width=True)

                # İndirme Butonu
                csv = df_new.to_csv(index=False).encode('utf-8')
                st.download_button("Analiz Sonuçlarını İndir (.CSV)", csv, "analiz_sonuclari.csv", "text/csv")