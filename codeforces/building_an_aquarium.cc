#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

bool filed(vector<int64_t> &a, int64_t h, int64_t x) {
  int64_t need = 0;
  for (auto i : a) {
    need += max(int64_t(0), h - i);
  }
  return need <= x;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    int64_t x = 0;
    std::cin >> n >> x;
    vector<int64_t> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    int64_t l = 0;
    int64_t r = x + a.front();
    int64_t res = -1;
    while (l <= r) {
      auto m = (l + r) / 2;
      if (filed(a, m, x)) {
        res = m;
        l = m + 1;
      } else {
        r = m - 1;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
