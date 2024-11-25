#include <iostream>

// O(1)
int main() {
  long long n, m, a;
  std::cin >> n >> m >> a;
  long long x = n / a;
  if (n % a) x++;
  long long y = m / a;
  if (m % a) y++;
  std::cout << x * y << "\n";
}
