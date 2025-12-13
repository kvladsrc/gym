#ifndef CPP_WARMUP_SPARSE_TABLE_H_
#define CPP_WARMUP_SPARSE_TABLE_H_

#include <cstddef>
#include <vector>

namespace warmup {

class sparse_table {
 public:
  explicit sparse_table(const std::vector<int>& v);
  int query(size_t l, size_t r) const;

 private:
  std::vector<std::vector<int>> table;
  std::vector<size_t> logs;
};

}  // namespace warmup

#endif  // CPP_WARMUP_SPARSE_TABLE_H_
