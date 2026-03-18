import pandas as pd
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.model_selection import train_test_split
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import accuracy_score, classification_report


def run_training():
    print("\n--- 3. AŞAMA: Yapay Zeka Eğitimi Başlıyor ---")

    file_path = "CSV/nlp_ready_reviews.csv"
    try:
        df = pd.read_csv(file_path, encoding='utf-8')
    except FileNotFoundError:
        print(f"Hata: Önce 2. aşamayı çalıştırıp '{file_path}' dosyasını oluşturmalısın!")
        return

    # NaN (Boş) kalan satırları son bir kez garantiye alıp siliyoruz
    df = df.dropna(subset=['NLP_Ready_Comment'])

    # X: Girdi (Yorumlar), Y: Çıktı (Duygu: 0 veya 1)
    X = df['NLP_Ready_Comment']
    Y = df['Sentiment']

    print("1. Kelimeler matematiğe (TF-IDF matrisine) dönüştürülüyor...")
    # max_features=5000: Sadece en çok kullanılan ve en belirleyici 5000 kelimeyi al (RAM'i şişirmemek için)
    vectorizer = TfidfVectorizer(max_features=5000)
    X_vectorized = vectorizer.fit_transform(X)

    print("2. Veri seti Eğitim (%80) ve Test (%20) olarak ikiye bölünüyor...")
    X_train, X_test, Y_train, Y_test = train_test_split(X_vectorized, Y, test_size=0.2, random_state=42)

    print("3. Model eğitiliyor (Logistic Regression)...")
    # Logistic Regression, metin sınıflandırmada inanılmaz hızlı ve başarılı bir modeldir.
    model = LogisticRegression()
    model.fit(X_train, Y_train)  # Makinenin ders çalıştığı satır tam olarak burası!

    print("4. Model sınava sokuluyor ve başarı ölçülüyor...\n")
    # Test verilerini modele soruyoruz
    Y_pred = model.predict(X_test)

    # Sonuçları Karşılaştır
    accuracy = accuracy_score(Y_test, Y_pred)
    print("=" * 40)
    print(f" SINAV SONUCU (DOĞRULUK ORANI): % {accuracy * 100:.2f}")
    print("=" * 40)

    print("\nDetaylı Karne:")
    # 0: Negatif, 1: Pozitif
    print(classification_report(Y_test, Y_pred, target_names=['Negatif (0)', 'Pozitif (1)']))

    # --- CANLI TEST KISMI ---
    print("\n--- MODELİ KENDİN TEST ET ---")
    while True:
        user_input = input("Bir cümle yazın (Çıkmak için 'q' veya 'çıkış' yazın): ")
        if user_input.lower() in ['q', 'çıkış']:
            break

        # Kullanıcının yazdığı cümleyi de aynı matematikten (vectorizer) geçirmeliyiz
        user_input_vec = vectorizer.transform([user_input])
        prediction = model.predict(user_input_vec)

        if prediction[0] == 1:
            print(">> Modelin Tahmini: POZİTİF 😊\n")
        else:
            print(">> Modelin Tahmini: NEGATİF 😡\n")