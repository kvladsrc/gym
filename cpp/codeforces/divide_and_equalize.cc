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

vector<int> factorize(int n, map<int, vector<int>> &table) {
  if (!table[n].empty()) {
    return table[n];
  }

  int div = 2;
  while (n % div != 0) {
    div++;
  }

  auto v = factorize(n / div, table);
  v.push_back(div);
  table[n] = v;
  return v;
}

void erato_fill(int maximal, map<int, vector<int>> &table) {
  vector<bool> erato(maximal + 1, true);
  for (int i = 2; i <= maximal; ++i) {
    if (!erato[i]) {
      continue;
    }

    for (int j = 2; i * j <= maximal; ++j) {
      erato[i * j] = false;
    }
  }

  for (int i = 1; i <= maximal; ++i) {
    if (erato[i]) {
      table[i] = {i};
    }
  }
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;

  map<int, vector<int>> factor_table;
  erato_fill(1000000, factor_table);

  while (t--) {
    int n;
    std::cin >> n;
    map<int, int> a;
    for (int i = 1; i <= n; ++i) {
      int buf;
      std::cin >> buf;
      a[buf]++;
    }

    map<int, int> mems;
    for (auto p : a) {
      auto v = factorize(p.first, factor_table);
      for (auto x : v) {
        mems[x] += p.second;
      }
    }

    bool we_can = true;
    for (auto p : mems) {
      if (p.first == 1) {
        continue;
      }

      if (p.second % n) {
        we_can = false;
        break;
      }
    }

    std::cout << (we_can ? "YES" : "NO") << "\n";
  }

  return 0;
}
