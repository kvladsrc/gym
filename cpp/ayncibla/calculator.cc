#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

struct Result {
  bool calculated;
  int actions;
  int prev;
};

static vector<int> reproduce_actions(int n, vector<Result> const& table) {
  vector<int> sequence;

  while (n != 1) {
    sequence.push_back(n);
    n = table[n].prev;
  }

  sequence.push_back(1);
  return sequence;
}

static int optimal_sequence(int n, vector<Result>& table) {
  if (n <= 1) {
    return 1;
  }

  if (table[n].calculated) {
    return table[n].actions;
  }

  int tmp = 0;

  if (n % 3 == 0) {
    tmp = optimal_sequence(n / 3, table);
    table[n].prev = n / 3;
  }

  if (n % 2 == 0) {
    auto case2 = optimal_sequence(n / 2, table);
    if (tmp == 0) {
      tmp = case2;
      table[n].prev = n / 2;
    } else if (tmp > case2) {
      tmp = case2;
      table[n].prev = n / 2;
    }
  }

  auto case3 = optimal_sequence(n - 1, table);
  if (tmp == 0) {
    table[n].prev = n - 1;
  } else if (tmp > case3) {
    table[n].prev = n - 1;
  }

  table[n].calculated = true;
  table[n].actions = table[table[n].prev].actions + 1;

  return table[n].actions;
}

int main() {
  int n = 0;
  std::cin >> n;
  vector<Result> table(n + 1);
  optimal_sequence(n, table);
  auto sequence = reproduce_actions(n, table);

  std::cout << sequence.size() - 1 << '\n';
  for (size_t j = sequence.size(); j > 0; --j) {
    std::cout << sequence[j - 1] << " ";
  }
}
