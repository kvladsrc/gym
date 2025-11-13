#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

vector<bool> make_erato(int n) {
  vector<bool> table(n + 1);
  int cur = 2;
  while (cur < n + 1) {
    if (table[cur]) {
      cur++;
      continue;
    }

    for (int i = cur + cur; i < n + 1; i += cur) {
      table[i] = true;
    }

    cur++;
  }

  return table;
}

int next_prime(int n, vector<bool> &erato) {
  for (size_t i = n + 1; i < erato.size(); i++) {
    if (!erato[i]) return i;
  }
  return 1;
}

int main(int argc, char *argv[]) {
  int n, m;
  std::cin >> n >> m;
  auto table = make_erato(m);
  if (m == next_prime(n, table)) {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
