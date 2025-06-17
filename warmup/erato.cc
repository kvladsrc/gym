#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

namespace {

/*
 * Fill the table by following values: a_i = true when and only when i
 * is a prime number.
 */
void erato_fill(vector<bool> &table) {
  const size_t size = table.size();

  if (size <= 2) {
    std::fill(table.begin(), table.end(), false);
    return;
  }

  std::fill(table.begin(), table.end(), true);

  table[0] = table[1] = false;

  // See comment for internal cycle.
  for (size_t p = 2; p * p < size; ++p) {
    if (!table[p]) {
      continue;
    }

    // No need to mark numbers < p^2, because every multiplier less
    // than p already marked due to previous steps.
    for (size_t j = p * p; j < size; j += p) {
      table[j] = false;
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
