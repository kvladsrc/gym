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

vector<pair<int, int>> all_pairs(const vector<int>& v) {
  vector<pair<int, int>> res;
  int size = v.size();
  for (int f = 0; f < size; ++f) {
    for (int s = f + 1; s < size; ++s) {
      res.push_back({v[f], v[s]});
    }
  }
  return res;
}

vector<int> all_divisors(int n) {
  vector<int> res;
  for (int i = 1; i * i <= n; ++i) {
    if ((n % i) == 0) {
      res.push_back(i);
      auto second = n / i;
      if (i != second) {
        res.push_back(second);
      }
    }
  }
  return res;
}

int gcd(int a, int b) { return (a % b == 0 ? abs(b) : gcd(b, a % b)); }

bool check(pair<int, int> p, int i) { return gcd(p.first + p.second, i) == 1; }

void erato(vector<pair<int, int>>& table) {
  int size = table.size();

  std::fill(table.begin(), table.end(), std::pair{-1, -1});

  table[0] = table[1] = {1, 1};

  for (int i = 2; i * i < size; ++i) {
    if (table[i].first != -1) {
      continue;
    }

    for (int j = i * i; j < size; j += i) {
      auto j_cpy = j;
      while ((j_cpy % i) == 0) {
        j_cpy /= i;
      }
      table[j] = {i, j_cpy};
    }
  }
}

bool erato_check(const vector<pair<int, int>>& erato_table, int i) {
  if (erato_table[i].first == erato_table[i].second ||
      erato_table[i].first == 1 || erato_table[i].second == 1) {
    return false;
  }
  return check(erato_table[i], i);
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;

  vector<int> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  vector<pair<int, int>> erato_table(10000001);
  erato(erato_table);

  for (auto i : a) {
    if (erato_check(erato_table, i)) {
      std::cout << erato_table[i].first << " ";
    } else {
      std::cout << -1 << " ";
    }
  }
  std::cout << "\n";

  for (auto i : a) {
    if (erato_check(erato_table, i)) {
      std::cout << erato_table[i].second << " ";
    } else {
      std::cout << -1 << " ";
    }
  }
  std::cout << "\n";

  return 0;
}
