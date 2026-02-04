#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    int64_t res = 0;
    for (int i = 0; i < n; ++i) {
      for (int mul = 1;; ++mul) {
        int j = mul * a[i] - (i + 1);
        if ((j - 1) >= i) break;
        if ((j - 1) < 0) continue;
        if (a[j - 1] == mul) res++;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
