#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

int edit_cost(const string& a, const string& b) {
  int res = 0;
  for (size_t i = 0; i < a.size(); ++i) {
    res += abs(a[i] - b[i]);
  }

  return res;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, m;
    std::cin >> n >> m;

    vector<string> in(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> in[i];
    }

    int min_diff = m * 100;
    for (int i = 0; i < n; ++i) {
      for (int j = i + 1; j < n; ++j) {
        int buf = edit_cost(in[i], in[j]);
        if (buf < min_diff) min_diff = buf;
      }
    }

    std::cout << min_diff << "\n";
  }

  return 0;
}
