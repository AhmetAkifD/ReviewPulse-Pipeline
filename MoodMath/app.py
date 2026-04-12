import shutil
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

if 'grafikleri_goster' not in st.session_state:
    st.session_state.grafikleri_goster = False

st.sidebar.title("🧠 MoodMath Paneli")
st.sidebar.markdown("---")
menu = st.sidebar.radio("İşlem Seçiniz:",
                        ("1. Veri Boru Hattı & Metrikler",
                         "2. Model Eğitimi & Ayarlar",
                         "3. Toplu Analiz (JSON/CSV)",
                         "4. Canlı Test & Şeffaf Analiz"))

if 'ilk_acilis' not in st.session_state:
    if os.path.exists("CSV"):
        shutil.rmtree("CSV")
        os.makedirs("CSV")

    st.session_state.analiz_hazir = False
    st.session_state.ilk_acilis = True

# --- 1. SAYFA: VERİ GÖRSELLEŞTİRME VE TEMİZLİK ---
if menu == "1. Veri Boru Hattı & Metrikler":
    st.title("📊 Veri Ön İşleme ve Metrikler")
    st.write("Verinizi yükleyin ve NLP'ye hazırlayın.")

    uploaded_raw_file = st.file_uploader("Dosyanızı yükleyin (CSV veya JSON):", type=['csv', 'json'])

    # Eğer kullanıcı bir dosya yüklediyse butonlar aktif olacak
    if uploaded_raw_file is not None:
        if uploaded_raw_file.name.endswith('.json'):
            raw_df = pd.read_json(uploaded_raw_file)
        else:
            raw_df = pd.read_csv(uploaded_raw_file)

        st.success(f"{len(raw_df)} satırlık veri başarıyla hafızaya alındı!")

        col1, col2 = st.columns(2)
        with col1:
            st.subheader("Adım 1: Temizlik (Regex)")
            if st.button("Yüklenen Veriyi Temizle", use_container_width=True):
                with st.spinner("Temizleniyor..."):
                    data_cleaner.run_cleaner(raw_df)
                    st.session_state.grafikleri_goster = True

                st.success("Temizlik Tamam!")

        with col2:
            st.subheader("Adım 2: NLTK Stopwords")
            if st.button("Etkisiz Kelimeleri Ayıkla", use_container_width=True):
                with st.spinner("Ayıklanıyor..."):
                    stopword_remover.run_nltk_cleaning()
                st.success("NLP'ye Hazır!")

    else:
        st.info("İşleme başlamak için lütfen yukarıdan bir veri seti yükleyin.")

    st.markdown("---")
    st.subheader("📌 Güncel Veri Seti Özeti")

    # Sadece butona basıldıysa grafikleri çiz
    if st.session_state.grafikleri_goster:
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

            # Plotly Express ile Şık Grafik
            st.write("**Duygu Dağılım Grafiği**")
            df_chart = pd.DataFrame({
                "Duygu": ["Negatif", "Pozitif"],
                "Yorum Sayısı": [neg_count, pos_count]
            })

            fig = px.bar(
                df_chart,
                x="Duygu",
                y="Yorum Sayısı",
                color="Duygu",
                color_discrete_map={"Negatif": "#ff4b4b", "Pozitif": "#21c354"},
                text="Yorum Sayısı"
            )
            fig.update_traces(width=0.3, textposition='outside')
            fig.update_layout(xaxis_tickangle=0, showlegend=False, margin=dict(t=30, b=10, l=10, r=10), height=400)

            st.plotly_chart(fig, use_container_width=True)
        except Exception as e:
            st.warning("Grafikleri görmek için 1. ve 2. adımları tamamlayarak veriyi hazır hale getirin.")
    else:
        st.info("Lütfen yeni bir dosya yükleyip 'Temizle' butonuna basarak analizleri başlatın.")

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
        with st.spinner("Model eğitiliyor..."):
            result = model_trainer.run_training(max_features=max_feat, test_size=test_ratio)

            if isinstance(result, tuple) and result[0] is not None:
                accuracy, _ = result
                st.success("Eğitim Başarılı! Model hafızaya kaydedildi.")
                st.metric("Sınav Başarısı (Accuracy)", f"% {accuracy * 100:.2f}")
            else:
                error_msg = result[1] if isinstance(result, tuple) else result
                st.error(error_msg)

# --- 3. SAYFA: TOPLU ANALİZ ---
elif menu == "3. Toplu Analiz (JSON/CSV)":
    st.title("📂 Toplu Veri Analizi")
    st.write("Dosyalarınızı yükleyin ve model ile toplu analiz gerçekleştirin.")

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
                with st.spinner("Analiz ediliyor..."):
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

# --- 4. SAYFA: CANLI TEST VE ŞEFFAF YAPAY ZEKA ---
elif menu == "4. Canlı Test & Şeffaf Analiz":
    st.title("🎯 Canlı Test ve Model Kararları")
    st.write("Metninizi yazın ve yapay zekanın analiz edip neden bu kararı verdiğini görün.")

    user_input = st.text_area("Test etmek istediğiniz yorumu buraya yazın:",
                              placeholder="Örn: Kargo çok hızlıydı ama ürün kutusu yırtık geldi, hiç beğenmedim...")

    if st.button("Analiz Et", type="primary"):
        # Model eğitilmiş mi diye kontrol et
        if not os.path.exists("Model/mood_model.pkl") or not os.path.exists("Model/mood_vectorizer.pkl"):
            st.error("⚠️ Önce 2. Sayfaya gidip modeli eğitmelisiniz!")
        elif len(user_input.strip()) < 3:
            st.warning("Lütfen daha uzun bir cümle yazın.")
        else:
            # Modeli ve Sözlüğü yükle
            model = joblib.load("Model/mood_model.pkl")
            vectorizer = joblib.load("Model/mood_vectorizer.pkl")

            with st.spinner("Cümle analiz ediliyor..."):
                # Kullanıcının metnini küçük harfe çevirip vektöre (sayılara) dönüştür
                # (İdealde kök bulucu da eklenebilir ama basit canlı test için yeterli)
                input_vector = vectorizer.transform([user_input.lower()])

                # Tahmin yap ve eminlik oranını (Olasılık) al
                prediction = model.predict(input_vector)[0]
                probability = model.predict_proba(input_vector)[0]

                st.markdown("---")

                # 1. BÖLÜM: SONUÇ
                if prediction == 1:
                    st.success(f"### 😊 POZİTİF YORUM")
                    st.write(f"**Yapay Zekanın Eminlik Oranı:** %{probability[1] * 100:.1f}")
                else:
                    st.error(f"### 😡 NEGATİF YORUM")
                    st.write(f"**Yapay Zekanın Eminlik Oranı:** %{probability[0] * 100:.1f}")

                # 2. BÖLÜM: ŞEFFAF ANALİZ (XAI)
                st.markdown("#### 🔍 Şeffaf Analiz (Model Neden Bu Kararı Verdi?)")
                st.write("Modelin bu kararı verirken dikkat ettiği kelimeler:")

                # Vektörün içindeki boş olmayan (modelin tanıdığı) kelimeleri bul
                feature_names = vectorizer.get_feature_names_out()
                nonzero_indices = input_vector.nonzero()[1]

                if len(nonzero_indices) == 0:
                    st.info("🤔 Model bu cümlede tanıdığı bir kelime bulamadı. Lütfen daha net kelimeler kullanın veya modelin kelime dağarcığını genişletin.")
                else:
                    # Modelin tanıdığı kelimeleri listele
                    words_found = [feature_names[i] for i in nonzero_indices]

                    # Şık bir şekilde ekrana bas
                    st.code(", ".join(words_found))