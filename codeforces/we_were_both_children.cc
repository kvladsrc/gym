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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    map<int, int> a;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      a[buf]++;
    }

    int mx = 0;
    vector<int> frogs(n + 1, 0);
    for (auto p : a) {
      int pos = p.first;

      while (pos <= n) {
        frogs[pos] += p.second;
        mx = max(mx, frogs[pos]);
        pos += p.first;
      }
    }

    std::cout << mx << "\n";
  }

  return 0;
}
