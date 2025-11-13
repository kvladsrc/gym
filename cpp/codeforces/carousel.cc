#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;

    vector<int> a(n);
    set<int> unique;

    for (auto &i : a) {
      std::cin >> i;
      unique.insert(i);
    }

    if (unique.size() == 1) {
      std::cout << 1 << "\n";
      for (auto _ : a) {
        std::cout << 1 << " ";
      }
      std::cout << "\n";
      continue;
    }

    if (a.size() % 2 == 0) {
      std::cout << 2 << "\n";
      bool f = true;
      for (auto _ : a) {
        if (f) {
          std::cout << 1 << " ";
        } else {
          std::cout << 2 << " ";
        }
        f = !f;
      }
      std::cout << "\n";
      continue;
    }

    bool need_three = true;
    for (size_t idx = 0; idx < n; ++idx) {
      if (a[idx] == a[(idx + 1) % n]) {
        need_three = false;
        break;
      }
    }

    if (!need_three) {
      std::cout << 2 << "\n";
      bool skipped = false;
      bool f = true;
      for (size_t idx = 0; idx < n; ++idx) {
        if (f) {
          std::cout << 1 << " ";
        } else {
          std::cout << 2 << " ";
        }

        if (a[idx] == a[(idx + 1) % n] && !skipped) {
          skipped = true;
          continue;
        }

        f = !f;
      }

      std::cout << "\n";
      continue;
    }

    std::cout << 3 << "\n";
    bool f = true;
    for (size_t idx = 0; idx < (n - 1); ++idx) {
      if (f) {
        std::cout << 1 << " ";
      } else {
        std::cout << 2 << " ";
      }
      f = !f;
    }
    std::cout << 3 << "\n";
  }

  return 0;
}
