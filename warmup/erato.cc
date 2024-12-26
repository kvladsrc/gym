#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

static void erato_fill(vector<bool> &table) {
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

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
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
