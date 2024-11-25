#include <algorithm>
#include <cstddef>
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

void erato_fill(vector<bool> &table) {
  for (size_t i = 2; i < table.size(); ++i) {
    if (!table[i]) {
      continue;
    }

    size_t next = i + i;
    while (next < table.size()) {
      table[next] = false;
      next += i;
    }
  }
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<bool> table(n, true);
  erato_fill(table);

  for (size_t i = 2; i < table.size(); ++i) {
    if (table[i]) {
      std::cout << i << " ";
    }
  }
  std::cout << "\n";

  return 0;
}
