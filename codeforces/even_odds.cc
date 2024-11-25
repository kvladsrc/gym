#include <iostream>

long long even_odds(long long n, long long k) {
  long long first_seq = n / 2 + n % 2;

  if (k <= first_seq) {
    return k * 2 - 1;
  } else {
    return (k - first_seq) * 2;
  }
}

int main(int argc, char *argv[]) {
  long long n, k;
  std::cin >> n >> k;
  std::cout << even_odds(n, k) << "\n";

  return 0;
}
