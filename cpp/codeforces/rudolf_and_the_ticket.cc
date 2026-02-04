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
    int n, m, k;
    std::cin >> n >> m >> k;

    vector<int> first(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> first[i];
    }
    sort(first.begin(), first.end());

    vector<int> second(m);
    for (int i = 0; i < m; ++i) {
      std::cin >> second[i];
    }
    sort(second.begin(), second.end());

    int res = 0;
    for (int i = 0; i < n && first[i] <= k; ++i) {
      for (int j = 0; j < m && first[i] + second[j] <= k; ++j) {
        res++;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
