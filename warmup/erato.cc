#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

namespace {

/*
 * Fill the table by following values: a_i = true when and only when i
 * is a prime number.
 */
void erato_fill(vector<bool> &table) {
  std::fill(table.begin(), table.end(), true);
  table[1] = false;

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

}  // namespace

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<bool> table(n + 1);

  erato_fill(table);

  for (size_t i = 2; i < table.size(); ++i) {
    if (table[i]) {
      std::cout << i << " ";
    }
  }
  std::cout << "\n";

  return 0;
}
