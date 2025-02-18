#include <cstdint>
#include <iostream>
#include <vector>

using std::vector;

namespace {

int64_t gorner(const vector<int64_t>& coefs, int64_t x) {
  int64_t res = 0;
  for (const auto c : coefs) {
    res *= x;
    res += c;
  }
  return res;
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  vector<int64_t> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  int64_t x = 0;
  std::cin >> x;

  std::cout << gorner(a, x) << "\n";

  return 0;
}
