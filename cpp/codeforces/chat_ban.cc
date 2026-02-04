#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <locale>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int64_t emotions_in_message(int64_t k, int64_t m) {
  if (m > (k + k - 1)) {
    return ((k - 1) * k) + k;
  }

  auto res = ((m + 1) * m) / 2;

  if (m > k) {
    auto extra = m - k;
    res -= (extra + 1) * extra;
  }

  return res;
}

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int64_t k, x;
    std::cin >> k >> x;

    int64_t messages_per_triangle = k + k - 1;
    auto emotion_per_triangle = emotions_in_message(k, messages_per_triangle);

    if (x >= emotion_per_triangle) {
      std::cout << messages_per_triangle << "\n";
      continue;
    }

    int64_t res = 1;
    int64_t l = 1, r = messages_per_triangle;
    while (l <= r) {
      int64_t mid = (l + r) / 2;
      auto local = emotions_in_message(k, mid);
      if (local >= x) {
        res = mid;
        r = mid - 1;
      } else {
        l = mid + 1;
      }
    }

    if (emotions_in_message(k, res) < x) {
      res++;
    }

    std::cout << res << "\n";
  }

  return 0;
}
