#include <algorithm>
#include <iostream>
#include <vector>

using std::vector;

struct present {
  int i, pi;
};

bool compare_present(const present& a, const present& b) { return a.pi < b.pi; }

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<present> table(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> table[i].pi;
    table[i].i = i + 1;
  }

  std::sort(table.begin(), table.end(), compare_present);

  for (int i = 0; i < n; ++i) {
    std::cout << table[i].i << " ";
  }

  return 0;
}
