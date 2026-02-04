#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
    unordered_map<int, int> m;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      m[buf]++;
    }

    int max_freq = 0;
    for (auto const& p : m) {
      if (p.second > max_freq) {
        max_freq = p.second;
      }
    }

    int distinct = m.size();
    int res = max(min(distinct - 1, max_freq), min(distinct, max_freq - 1));
    std::cout << res << "\n";
  }
  return 0;
}
