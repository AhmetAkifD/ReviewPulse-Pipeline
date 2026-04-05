import streamlit as st
import pandas as pd
import data_cleaner
import stopword_remover
import model_trainer

# --- SAYFA AYARLARI ---
st.set_page_config(page_title="MoodMath NLP", page_icon="🧠", layout="wide")

# --- YAN MENÜ (SIDEBAR) ---
st.sidebar.title("🧠 MoodMath Paneli")
st.sidebar.markdown("---")
menu = st.sidebar.radio("İşlem Seçiniz:",
                        ("1. Veri Boru Hattı (Pipeline)",
                         "2. Model Eğitimi",
                         "3. Canlı Test Merkezi"))

# --- 1. SAYFA: VERİ TEMİZLİĞİ ---
if menu == "1. Veri Boru Hattı (Pipeline)":
    st.title("🧹 Veri Ön İşleme (Data Preprocessing)")
    st.write("Ham CSV verilerinizi NLP modeline uygun hale getirin.")

    col1, col2 = st.columns(2)

    with col1:
        st.subheader("Adım 1: Regex & Temizlik")
        if st.button("Ham Veriyi Temizle", use_container_width=True):
            with st.spinner("Veriler temizleniyor..."):
                data_cleaner.run_cleaner()
            st.success("1. Aşama Tamamlandı! (cleaned_reviews.csv oluşturuldu)")

            # Veriden bir parça gösterelim
            try:
                df_clean = pd.read_csv("CSV/cleaned_reviews.csv")
                st.dataframe(df_clean.head(), use_container_width=True)
            except:
                pass

    with col2:
        st.subheader("Adım 2: NLTK Stopwords")
        if st.button("Etkisiz Kelimeleri Ayıkla", use_container_width=True):
            with st.spinner("Stop-word'ler siliniyor..."):
                stopword_remover.run_nltk_cleaning()
            st.success("2. Aşama Tamamlandı! (nlp_ready_reviews.csv oluşturuldu)")

            try:
                df_ready = pd.read_csv("CSV/nlp_ready_reviews.csv")
                st.dataframe(df_ready.head(), use_container_width=True)
            except:
                pass


# --- 2. SAYFA: MODEL EĞİTİMİ ---
elif menu == "2. Model Eğitimi":
    st.title("⚙️ Yapay Zeka Model Eğitimi")
    st.write("Temizlenmiş verilerle Logistic Regression modelini eğitin.")

    if st.button("🚀 Eğitimi Başlat (Train Model)"):
        with st.spinner("Model eğitiliyor, TF-IDF matrisi oluşturuluyor..."):
            # Not: model_trainer'ın çıktılarını ekrana basmak için ileride ufak bir ayar yapabiliriz
            # Şimdilik arka planda senin kodunu çalıştıracak.
            try:
                model_trainer.run_training()
                st.success("Model başarıyla eğitildi ve Sınavı geçti!")
                st.info("Detaylı doğruluk oranlarını konsol penceresinden görebilirsiniz.")
            except Exception as e:
                st.error(f"Eğitim sırasında bir hata oluştu: {e}")


# --- 3. SAYFA: CANLI TEST MERKEZİ ---
elif menu == "3. Canlı Test Merkezi":
    st.title("🎯 MoodMath Canlı Analiz")
    st.write("Modelin neler öğrendiğini test edin!")

    # Kullanıcıdan metin alma kutusu
    user_input = st.text_area("Analiz edilecek yorumu buraya yazın:", height=150,
                              placeholder="Örn: Bu ürünü hiç beğenmedim, kargolama da çok kötüydü...")

    if st.button("Duyguyu Analiz Et", type="primary"):
        if user_input.strip() == "":
            st.warning("Lütfen bir şeyler yazın!")
        else:
            # BURASI İÇİN UFAK BİR KOD GÜNCELLEMESİ GEREKECEK!
            # Eğitilmiş modeli ve vectorizer'ı model_trainer'dan import edip kullanmamız lazım.
            st.info(
                "Bu özelliğin çalışması için `model_trainer.py` dosyasında modeli dışa aktarmamız (kaydetmemiz) gerekecek. (Detaylar aşağıda)")