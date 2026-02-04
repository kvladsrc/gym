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
#include <utility>
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

pair<int, int> lcs(const vector<int>& a) {
  map<int, int> m;
  int best_end = 0;
  for (auto i : a) {
    m[i] = m[i - 1] + 1;
    if (m[i] > m[best_end]) {
      best_end = i;
    }
  }

  return {best_end, m[best_end]};
}

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<int> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  auto p = lcs(a);
  auto need = p.first - (p.second - 1);
  std::cout << p.second << "\n";
  for (size_t idx = 0; idx < a.size(); ++idx) {
    if (a[idx] == need) {
      std::cout << idx + 1 << " ";
      need++;
    }
  }
  std::cout << "\n";

  return 0;
}
