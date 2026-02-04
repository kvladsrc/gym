#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    // Player with the maximal amount of tokens wins. Any player can
    // gain all tokens from players with lower amount or same amount
    // of tokens, he can win if after the operation he had more or
    // equal tokens as any player who can win.

    auto a_cpy = a;
    sort(a.begin(), a.end());
    vector<int64_t> pref_sum(n + 1);
    pref_sum.front() = 0;
    for (int idx = 1; idx <= n; ++idx) {
      pref_sum[idx] = pref_sum[idx - 1] + a[idx - 1];
    }

    int not_enought = 0;
    for (int idx = a.size() - 2; idx >= 0; --idx) {
      if (pref_sum[idx + 1] < a[idx + 1]) {
        not_enought = a[idx];
        break;
      }
    }

    vector<int> res;
    for (int idx = 0; idx < n; ++idx) {
      if (a_cpy[idx] > not_enought) {
        res.push_back(idx + 1);
      }
    }

    std::cout << res.size() << "\n";
    for (auto pos : res) {
      std::cout << pos << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
