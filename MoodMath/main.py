import data_cleaner
import stopword_remover
import model_trainer
import sys


def main_menu():
    while True:
        print("=" * 45)
        print(" MOODMATH - Doğal Dil İşleme Kontrol Paneli ")
        print("=" * 45)
        print("1. Ham Veriyi Temizle (Regex & Pandas)")
        print("2. Etkisiz Kelimeleri Ayıkla (NLTK)")
        print("3. Yapay Zeka Modelini Eğit ve Test Et")
        print("4. Çıkış")
        print("=" * 45)

        secim = input("Lütfen çalıştırmak istediğiniz adımı seçin (1/2/3/4): ")

        if secim == '1':
            data_cleaner.run_cleaner()
        elif secim == '2':
            stopword_remover.run_nltk_cleaning()
        elif secim == '3':
            model_trainer.run_training()
        elif secim == '4':
            print("Programdan çıkılıyor. Görüşmek üzere!")
            sys.exit()
        else:
            print("Geçersiz seçim! Lütfen 1, 2, 3 veya 4 girin.\n")


if __name__ == "__main__":
    main_menu()