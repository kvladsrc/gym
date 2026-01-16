#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    int zero_num = 0;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
      if (i == 0) {
        zero_num++;
      }
    }

    int lead_zero = 0;
    for (int i = 0; i < n; ++i) {
      if (a[i] == 0) {
        lead_zero++;
        continue;
      }
      break;
    }

    int end_zero = 0;
    for (int i = 0; i < n; ++i) {
      if (a[n - 1 - i] == 0) {
        end_zero++;
        continue;
      }
      break;
    }

    if (zero_num == 0) {
      std::cout << 1 << "\n";
      continue;
    }

    if (zero_num == n) {
      std::cout << 0 << "\n";
      continue;
    }

    if (zero_num == lead_zero + end_zero) {
      std::cout << 1 << "\n";
      continue;
    }

    std::cout << 2 << "\n";
  }

  return 0;
}
